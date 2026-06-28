# ADR-005: First-Class API Telemetry Type and Ingestion Access Control

**Date:** 2026-06-28
**Status:** Proposed

## Decision

1. **Make API a first-class telemetry type**, following the existing Desktop/Mobile precedent — its own ingestion endpoint (`POST /api/telemetry/api`), its own `ApiEventPayload`, its own `ApiEvent` entity, its own **health-centric dashboard**, and its own SDK entrypoint. API telemetry is *not* routed through the Web channel.
2. **Data model = auto-captured core + optional consumer-defined dimensions** (below). Caller identity is one *optional* dimension, never required.
3. **Add ingestion-endpoint access control** — an optional trusted-submitter IP allowlist on `POST /api/telemetry/*` (transport-IP gated; reject, never store; optionally record a security event).

**Cross-cutting convention established here:** per-site configuration is stored as **nullable typed columns on the `Site` entity**, with **global defaults cached in memory** and used when a site's column is null. Adding those columns to an existing database is handled by the idempotent startup reconciler (ADR-008). (Soft delete, the other convention surfaced during this design, is ADR-006.)

> **Supersedes** the earlier draft of this ADR, which proposed reusing the Web channel + a `SiteType=API` reclassification. That was over-indexed on avoiding pipeline duplication; see "Why first-class" below.

## Context

- **The trigger.** Live web bot-detection (`Api/TelemetryEndpoints.cs` lines 78-90) flags any request missing `Accept-Language` as `no-language` bot traffic. Machine callers (the .NET `HttpClient`, `curl`) never send it, so API telemetry routed through the Web channel is **all** misclassified as bots — observed in testing: an API-type workload routed through the Web channel showed Sessions = 0, all events in the Bots bucket.
- **The product already treats platforms as first-class peers.** Desktop and Mobile each have their own endpoint, payload, `Enriched*Event`, and dashboard. Forcing API into Web would make API the lone special-case — the inconsistency that makes it feel "less friendly" for a generic, self-hosted product.
- **The cross-cutting infrastructure is already shared via DI services**, not via the payload shape: `ApiKeyValidationFilter`, `GeoLocationService`, `VisitorHashService`, and the publisher pipeline are reused by all existing handlers. A fourth handler reuses all of it.

## Why first-class (and not the Web channel)

The data model genuinely differs — this is not cosmetic:

- An API consumer wants **method, route template, status, latency, caller identity, outcome**. The Web payload carries **none** of method/route-template/latency/caller, and *does* carry **referrer, language, browser/OS, first-visit/session** semantics that are meaningless for an API.
- So reusing Web both *omits* the useful API fields and *imposes* irrelevant browser ones. The web `page_view`/session model can't express good API telemetry.
- The right axis is **share infrastructure, separate contract/model/view** — which the DI-service structure already makes cheap. There is little real duplication to avoid.

Honest cost of going first-class: a new `ApiEvent` table, created on existing databases by the startup reconciler (ADR-008). This is more work than the rejected reclassification, but produces correct, purpose-built data.

## Data model

### Auto-captured core (SDK sends; consumer writes no code)

`route template` (e.g. `/license/{id}`, **never** the raw id — cardinality), `method`, `status code`, `latency`, `country` (coarse geo), `timestamp`. This alone covers the stated priority — **API usage and health**: volume, 2xx/4xx/5xx split, error rate, latency, top endpoints.

### Optional dimensions (consumer-defined, opt-in)

An open `key → value` bag the developer fills with 0, 1, or many values. Design as **dimensions** (low-cardinality, for grouping/charting), with **one** high-cardinality identifier for drill-down:

| Dimension | Cardinality | Use |
|---|---|---|
| **Machine fingerprint hash** | high (identifier) | drill into a specific problematic device; join to your DB |
| **App version** | low | spot stragglers, correlate errors to releases |
| **Account / plan type** | low | usage by segment without identifying anyone (preferred over license key or domain) |
| **Platform / OS** | low | where problems cluster |
| **Outcome** | low | the *why* behind a status (`valid`/`expired`/`revoked`/`seat-exceeded`; `ok`/`rate-limited`/`quota-exceeded`) — the high-value 4xx enrichment |

Mental model: **one high-cardinality identifier (machine hash) you filter by + a few low-cardinality dimensions you chart by.**

### Telemetry, not logging

Out of scope by design: request/response **payloads**, raw email/license-key/IP, stack traces, error messages. Those belong in the debug-log upload, not telemetry.

## Privacy / GDPR

GDPR attaches to **personal data, not to the channel** — API is not automatically exempt:

- API carrying only machine/tenant ids → effectively no personal data → low exposure.
- A *user* identifier (or IP) is personal data, web or API alike.
- Commercial-API customers under a user agreement give a **lawful basis** to process — it does not make the data non-personal.

Safe-by-default: reuse the Web privacy posture — **discard raw IP**, **hash identifiers client-side** (the SDK already mandates this), treat dimensions as **opaque** (never enrich them), and document "do not put raw PII in dimensions." Identity is optional, so a consumer can send nothing personal at all.

## Health-centric dashboard

The API view centers on **health + volume** — request volume over time, status split (2xx / **4xx rejected** / **5xx server error**), error-rate %, latency, top endpoints, and an **outcome breakdown on the 4xx** (normal rejections vs. abuse). This is a purpose-built view, distinct from Web's sessions/visitors — reinforcing why API is its own type.

## Detection on the API channel

Because API has its own channel, browser bot heuristics (`no-language`, UA bot-token) simply **do not apply** — the payload has no such fields. What carries over from ADR-004 is **Axis 2 (behavioral security events)**, which are channel-agnostic: request flooding, path probing, geo/IP anomaly still apply and may be tuned **stricter** for APIs (narrow legitimate surface → any probe is more suspicious). The ingestion allowlist below adds a further gate.

## Ingestion access control (trusted-submitter allowlist) — *optional, lowest priority*

**This gates the transport IP only — it has nothing to do with anything in the payload.** Critically, **API telemetry carries no caller IP** (telemetry, not logging — see "Telemetry, not logging" above), so for the API channel there is no "payload IP" at all; there is only the transport IP.

- **Transport IP** — the TCP source of the telemetry POST, observed by the server from the connection (or `CF-Connecting-IP`). For SDK middleware this is **always the submitting app server**. **This allowlist gates the transport IP.**
- **Payload IP** — a web-channel concept only (the raw IP the Web SDK sends for hashing/geo, then discarded). It does **not** exist on the API channel and is *not* what this gates.

**Honest scope:** the entire value of this feature is **leaked-key containment** — if a site's API key is stolen, a thief cannot submit forged telemetry from an arbitrary box. It depends on two operator-side conditions holding: the submitting app server's **egress IP is static**, and the **Cloudflare-only boundary** is intact (otherwise `CF-Connecting-IP`/`X-Forwarded-For` is spoofable). Because of those prerequisites it is **defense-in-depth, not a primary gate**, and in practice will be used mainly by larger orgs or savvy self-hosters. It is therefore the **last, optional** piece of this ADR (Phase 3) and may be deferred indefinitely. Its setup (Cloudflare boundary, static egress, configuring the allowlist) belongs in a dedicated deployment/architecture doc written when this phase is built.

Behavior:
- Resolve the transport IP via existing `GeoLocationService.GetClientIp` (`CF-Connecting-IP` → `X-Forwarded-For` → socket). **Sound only because the origin firewall is Cloudflare-only** — otherwise the forwarded header is spoofable. Self-hosters must ensure an equivalent boundary (documented prerequisite).
- `ApiKeyValidationFilter` already loads the site → check transport IP against the site's allowlist column (else the cached global default). Configured but no match → `403`, store nothing; optionally record an "unauthorized ingestion" security event.
- Reuse the existing `CidrRange` parser (extract the list-matching out of `IpFilterService` so it can serve more than the single `Filter:IgnoredIps` ruleset).

**Scope of value (precise):** keyless probes are *already* 401'd at `ApiKeyValidationFilter` and never stored, so this does **not** change classification of real traffic. Its value is narrow but real: **leaked-key containment** + dropping authenticated-but-off-server abuse. **Caveats:** submitter egress IP must be **static**; the Cloudflare-only boundary must hold.

## Per-site configuration (typed columns on `Site`, not KV)

Per-site config is stored as **nullable typed columns on `Site`**, added safely to existing databases by the reconciler (ADR-008). Resolution is **site column first; if null, fall back to a global default cached in memory**.

- **Per-site value:** a nullable `Site` column (e.g. `IngestAllowlist`).
- **Global default:** a single value (an `appsettings` entry or one global `ServerSetting`) loaded into memory and refreshed periodically **and** immediately when an admin saves it — the same cache pattern `IpFilterService` already uses. No per-request DB hit for the default.

Rationale: a single-server operator sets one global value; a multi-host operator overrides per site. Columns cost **zero extra reads** (handlers/filter already load the `Site` row) and are strongly typed. **Going-forward rule:** per-site knobs are nullable `Site` columns + a cached global default. (ADR-004's per-site thresholds should use this too.)

## Schema impact

| Piece | Mechanism | Schema change? |
|---|---|---|
| `ApiEvent` storage | new entity/table (mirrors `EnrichedDesktopEvent`/`EnrichedMobileEvent`) | **Yes** — created by the reconciler (ADR-008) |
| `SiteType=API` | append enum value (existing column) | None |
| Per-site ingestion allowlist | nullable `Site` column | **Yes** — added by the reconciler (ADR-008) |
| Global config defaults | `appsettings` / single `ServerSetting`, cached in memory | None |

## SDK / Admin

- **SDK packaging (open question):** Web and API are both ASP.NET (same host), so either a peer package (`FactFoundry.TelemetryForge.Api`, consistent with Desktop/Mobile) or an API entrypoint in the existing ASP.NET package (`AddTelemetryForgeApi`/`UseTelemetryForgeApi`). Either gives a clear "I have an API" path. Decide at implementation. (Lives in the separate `telemetry-forge-sdk` repo — the server endpoint has no client until it ships.)
- Registration dropdown gains **API**. Site `Type` is **not** editable: a site registered as the wrong type is **deleted and re-registered** as the correct type (using the ADR-006 delete flow). No in-place conversion — converting would only strand the old type's events under a now-different-type site anyway.

## Phasing

1. **Phase 1** — API ingestion endpoint + `ApiEventPayload` + `ApiEvent` + auto-core capture + health dashboard + SDK entrypoint. Resolves the misclassification by giving API its own correct home. (Carries the new-table schema caveat.)
2. **Phase 2** — optional dimensions (machine hash, version, plan, platform, outcome) + dashboard grouping/filtering.
3. **Phase 3 (optional, may be deferred indefinitely)** — ingestion allowlist (per-site column + cached global default) + admin UI + "unauthorized ingestion" security event + the deployment/architecture doc covering the Cloudflare boundary and static-egress prerequisites.

## References

- ADR-003 — per-request web events.
- ADR-004 — bot/security classification (API inherits Axis 2 behavioral detectors; browser heuristics N/A).
- ADR-006 — soft delete over hard delete (and the "also delete all associated records" flow used when re-registering a wrong-type site).
- ADR-007 — active health-check/uptime monitoring (the complementary "is it down" half of API health).
- ADR-008 — idempotent startup schema reconciler (creates `ApiEvent` and adds the per-site config column on existing databases).
- `Api/TelemetryEndpoints.cs` lines 78-90 — the browser bot detection that misclassified API traffic.
- `Api/ApiKeyValidationFilter.cs` — resolves the site (extend to load it and check the allowlist column).
- `Services/IpFilterService.cs` / `CidrRange` — extract list-matching for reuse; `Data/Entities/Site.cs` — new config columns.
