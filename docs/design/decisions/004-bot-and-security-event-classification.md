# ADR-004: Bot and Security Event Classification

**Date:** 2026-05-31
**Status:** Proposed

## Decision

Stop treating all non-human / anomalous traffic as a single binary `IsBot` flag. Split detection into two independent axes:

1. **Client identity** — *what the client is*
2. **Security events** — *what a session did* (behavioral signals), each with a risk severity

Risk is computed from the combination, not from a single label, and **identity gates behavior**: a client whose bot identity is *IP-verified* is marked a known bot and is exempt from security-event detection (a real, proven Googlebot crawling fast is expected, not an alert). Clients that only *claim* a bot identity via User-Agent — with no IP proof — are still subject to detection, so a spoofed UA gets no free pass. The admin UI separates the benign "who's crawling me" view from the "what looks like an attack" view.

## Context

Detection currently produces one boolean (`WebEvent.IsBot`) plus a free-text `BotReason` with five values:

- `user-agent` — UA string contains `bot`/`crawl`/`spider`/`curl`/`wget`/`httpie`
- `no-language` — missing Accept-Language
- `country-hop` — session seen from 3+ countries
- `page-velocity` — 5+ page_views in 60s
- `path-scan` — same filename requested from 5+ directories (vuln scanning)

Two problems:

- **"Bot" is the wrong word for half of these.** `country-hop`, `path-scan`, and `page-velocity` describe *behavior*, not identity. A scripted scan wearing a normal-browser UA is arguably more dangerous than an honest crawler, but today it's labeled the same as Googlebot — or worse, missed because its UA looks human.
- **No notion of risk.** A legitimate crawler (Googlebot, Claude-User, Bingbot) and a vulnerability scanner are both just "bot," shown identically on the Security page.

## Model

### Axis 1 — Client identity

| Type | Meaning | Default risk |
|---|---|---|
| **Human** | Normal browser traffic | — |
| **Verified Bot** | Declared crawler/agent whose identity is **proven** by IP/CIDR or FCrDNS (Googlebot, Bingbot, GPTBot, …) | Low |
| **Claimed Bot** | Declares a known agent via UA token, but identity is **unverified** (vendor publishes no ranges, or check unavailable) | Low–Medium |
| **Automated** | Looks non-human (generic "bot" UA, headless, no Accept-Language) but makes no specific identity claim | Low–Medium |

Bots are identified first by a curated User-Agent token table (`KnownBotService`), then that claim is **verified against the operator's network** (below). The UA tells us what it *claims to be*; the IP tells us if that's *true*.

#### Confidence ladder

- **Verified** — UA claims a known agent **and** the client IP matches that operator's published range (or passes FCrDNS). Top confidence → `Verified Bot`, Low risk.
- **Claimed, unverified** — UA claims a known agent but the operator publishes no ranges and FCrDNS is unavailable. A notch lower → `Claimed Bot`.
- **Impersonator** — UA claims a known agent but the IP is **not** in that operator's range. This is dispositive proof of spoofing → **escalate to a Security Event** (no behavioral inference needed).

This gradient is exactly the granularity the old binary `IsBot` could not express.

#### Verifying identity (defeats UA spoofing)

Anyone can set their UA to `Googlebot`; thousands of scrapers do daily. Two mechanisms, used together:

1. **Published CIDR feeds (primary, inline).** Major operators publish their crawler IP ranges as JSON (e.g. Googlebot, OpenAI's GPTBot/ChatGPT-User, Bing, Perplexity). We **mirror these feeds on a timer** into a local reference table and do a synchronous in-memory CIDR match at ingestion — same window as the geo lookup, no network call. This fits our discard-the-IP model perfectly: derive the verdict, write `BotName` + verified flag, drop the raw IP. We do **not** maintain individual IPs — the vendors rotate, we re-sync the feed.
2. **FCrDNS (fallback, scoped).** For operators that don't publish ranges (sources conflict on Anthropic/ClaudeBot), forward-confirmed reverse DNS — PTR ends in the operator's domain, then a forward lookup resolves back to the same IP. It needs the raw IP, so it runs only where the IP is still available: either synchronously inline within the ingestion window, or against short-retained IPs (see Retention) for non-clean traffic. Scope it to the sliver that the ASN pre-filter passes but CIDR can't confirm, so the DNS cost is bounded.

**ASN pre-filter (optimization, double duty).** A single IP→ASN lookup at ingestion (e.g. Google = AS15169 / AS396982) eliminates 95%+ of fake crawlers before any DNS work, **and** serves as the datacenter-vs-residential signal. One in-memory lookup → two booleans.

**Privacy note:** the reference table stores only **public** data (operator CIDR ranges → bot identity), never a log of observed client IPs. Per-event we keep only the derived `BotName`/verified verdict — except where the Retention policy below deliberately holds raw IPs briefly for non-clean traffic.

### Axis 2 — Security events (behavioral)

Flags that fire on any client **except IP-verified bots** (which are gated out per the Decision). A browser-looking UA, an `Automated` client, or a merely *claimed* bot is all fair game:

| Event | Was | Severity |
|---|---|---|
| **Path probing** | `path-scan` | High |
| **Geo / IP anomaly** | `country-hop` | High |
| **Request flooding** | `page-velocity` | Medium |

Severity is derived from the event type via a central `BotClassifier`, so it is consistent everywhere and requires no stored severity column or backfill.

**Composition rule (the only one needed):** if `identity == Verified Bot` → skip all detectors. Otherwise run them. This replaces a full identity×severity matrix — the only cell that mattered was suppressing security events for proven crawlers, and a hard gate does that cleanly.

**Dependency / caveat:** `path-scan` and `page-velocity` are per-session counters, so they are only as reliable as session attribution. If one visitor's events fragment across multiple sessions, a scan can spread below the threshold and the detector under-fires. Confirm session grouping is sound before trusting these counts. *(This is a flagged assumption, not a confirmed bug.)*

### Vocabulary

- "**Bot**" is reserved for identified/declared agents (the benign, low-risk case).
- Behavioral detections are "**Security Events**" — a security concern, not a bot taxonomy.

## Storage

- Derive **identity category** and **severity** from existing signals via `BotClassifier` — no new severity column, no backfill (every existing `country-hop` row reads as High immediately).
- Add **one** nullable `BotName` column on `WebEvent` / `EnrichedWebEvent` for the identified agent name (e.g. "Googlebot") — it cannot be derived because the raw User-Agent is not persisted.
- Keep `IsBot` and `BotReason` for backward compatibility; `BotReason` gains `known-bot`.
- Existing rows previously tagged `user-agent` are re-identified best-effort (from the parsed `Browser`) by the existing **"Scan for Bots"** button, which is extended to set `BotName`.
- **Schema-update risk (must resolve before Phase 1 ships).** The project currently has *no* EF migrations and rides `EnsureCreated`, which will not add a new column to an existing database. Adding `BotName` manually works for our single instance but **silently breaks self-hosters on upgrade** — they pull the release and the feature dies with no error. Since `BotName` lands in Phase 1, this needs a real fix: author a migration (or a startup schema-check that adds the column if missing). This is a **project-wide gap** that `IsBot`/`BotReason` already share, not unique to this ADR — flagging it here because this is the first feature shipped to self-hosters that depends on it.

### Verified-bot reference data

- A **local reference table** of *public* operator CIDR ranges → bot identity, mirrored from the vendors' published JSON feeds on a timer (a hosted service, like `SessionMaterializationService`). This is the IP-analog of `KnownBotService`'s UA table.
- Store only the **public reference data** and the per-event **derived verdict** (`BotName` + verified flag). Never store observed client IPs.
- Optionally cache the last-fetched feeds on disk so a restart doesn't depend on vendor availability.

## IP retention policy (tiered)

Today's hard rule is "raw IPs are never persisted." This ADR **deliberately relaxes** that to a tiered policy, because deeper verification and analysis need the IP that an immediate discard throws away:

- **Clean / verified traffic** → discard the IP immediately, as today. (Humans, IP-verified bots — nothing to investigate.)
- **Unknown / suspect traffic** → retain the raw IP briefly in a short-lived store with a TTL (default ~2–7 days, configurable), then auto-purge. This is what enables scoped FCrDNS and the deep-scan service below.

Notes:
- This is a **policy change to a documented guarantee** (CLAUDE.md: "Raw IP addresses are never persisted"). It must be called out explicitly, be configurable (including "off" to keep the strict old behavior), and the retention window documented for operators/self-hosters.
- Retention is scoped to *non-clean* traffic only — clean traffic still never touches disk with an IP.
- A hashed/truncated IP is not a substitute here: CIDR/FCrDNS verification needs the real address.

## Extensibility & updates

Two distinct concerns, handled differently — one is data, the other is logic.

### Bot intelligence — a data pack (no deploy)

Known-bot definitions (UA tokens + operator CIDR ranges) are **data**, so they update without a code release. Layered resolution, **operator intent wins**:

1. **Baked-in defaults** (lowest) — an embedded JSON resource shipped in the assembly. Works offline, zero config.
2. **Live feed sync** — the timer job pulls vendor JSON on top of the defaults.
3. **Local override** (highest) — an operator file (or DB table) that adds/overrides/pins entries. It beats the feed, so the next sync can't silently clobber a deliberate correction.

Updating the default is editing JSON and republishing the package — not rewriting logic. The pack can be versioned independently (optionally its own NuGet, e.g. `FactFoundry.TelemetryForge.BotIntel`), but runtime updates come from the override/feed layers, not from swapping the DLL.

### Security events — pluggable detectors (logic)

Detection behavior is **logic** and can't be fully data-driven, so it is structured for flexibility by tier:

1. **Tunable thresholds** — each shipped detector's parameters (e.g. 3 countries, 5 views/60s, 5 directories) are externalized to settings, so sensitivity is tuned **without a deploy**.
2. **Rule shapes** — common behavioral patterns (count-distinct-over-session, rate-over-window, group-count-threshold) are expressed as a small rule schema; new rules of an existing shape become **data**.
3. **Novel detectors** — genuinely new detection types (e.g. credential stuffing) are real code: an `ISecurityEventDetector` implementation registered in a detector registry, shipped in a release.

A full external rule engine / Roslyn scripting is possible but heavy; deferred unless threshold-rule churn justifies it.

### Deep-scan service (optional, out-of-band)

The ingest server stays lightweight: inline known-bot ID, CIDR verification, and the basic per-session detectors above — fast and privacy-safe. Heavier analysis runs as a **separate, optional service** over the short-retained raw IPs/logs (see Retention): FCrDNS, cross-session correlation, IP-reputation, and richer bot discovery. It writes verdicts back (e.g. promotes `Claimed` → `Verified`, flags new bots) and never puts that cost on the request path. The current "Scan for Bots" button is the in-process seed of this; it can grow into a standalone worker/server.

## UI / Dashboards

- **Security becomes a top-level nav group** (like Settings) with two sub-pages:
  - **Bots** (`/security/bots`) — verified crawlers: counts + breakdown. "Who's crawling me." Low-key.
  - **Security Events** (`/security/events`) — behavioral hits ranked by severity, with risk-colored chips, what they did, and the session. The actual alert view.
- **Event Stream** — the single "Bot" chip becomes risk-colored: green "Bot" (verified) / amber "Automated" / red "Security Event".
- **Main Dashboard & Analytics** — unchanged; remain human-only (exclude verified bots and security events, as today).
- **Data API** — expose identity category, severity, and bot name; optional severity filter.

## Consequences

- One source of truth (`BotClassifier`) replaces the scattered `GetReasonColor` / inline reason logic.
- Behavioral detection is decoupled from UA identity, so scripted scans behind real-browser UAs are caught and ranked, not mislabeled or missed.
- The Security page stops conflating two different audiences (a footnote vs. an alert).
- `KnownBotService` UA table and the CIDR feed list need occasional maintenance as new agents appear.
- The IP-verification step adds a feed-mirroring background job and an ASN dataset, but stays inside the discard-the-IP privacy model (synchronous in-memory checks only on the hot path).

## Phasing

The verification work is independent of the classification/UI rebuild and can land in stages:

1. **Phase 1** — two-axis model, `BotClassifier`, `KnownBotService` (UA-token), `BotName`, Security split + nav. Identity confidence is "claimed" only; the verified-bot gate has nothing to gate on yet, so detectors run on all non-human traffic. **Prerequisite:** resolve the schema-update gap above (migration / startup schema-check) since `BotName` ships here.
2. **Phase 2** — published-CIDR verification + ASN pre-filter inline; promotes "claimed" → "verified", activates the verified-bot detector gate, and flags impersonators as Security Events.
3. **Phase 3** — tiered IP retention + the deep-scan service, including scoped FCrDNS for operators without published ranges.

## Future

- Additional security event types (credential stuffing, known-bad IP reputation).
- Datacenter-vs-residential signal from the same ASN lookup.
- Possible per-site severity thresholds / alerting on the sinks pipeline.

## References

- Googlebot published IP ranges (JSON/CIDR): https://developers.google.com/search/apis/ipranges/googlebot.json
- Googlebot fraud detection / verification guide: https://almcorp.com/blog/googlebot-fraud-detection-prevention-guide/
- AI user-agents landscape 2026: https://nohacks.co/blog/ai-user-agents-landscape-2026
- Understanding AI crawlers: https://www.performanceliebe.de/en/blog/understanding-ai-crawlers/
