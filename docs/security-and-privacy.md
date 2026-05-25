# TelemetryForge — Security & Privacy Model

This document describes how TelemetryForge handles identity, hashing, data isolation, and privacy throughout the ingestion pipeline.

---

## Design Principles

1. **No raw identifiers are persisted** — IP addresses, session IDs, and cookie values are hashed before storage. The raw values exist only in memory during request processing.
2. **Hashes are purpose-scoped** — different hashing strategies are used for session grouping vs. visitor identity, and the two cannot be cross-referenced.
3. **Daily salt rotation** — session-scoped hashes include a daily-rotating salt, preventing long-term tracking from session data alone.
4. **No cookies set by the library** — TelemetryForge never sets cookies. The optional `_ga` cookie is read (not written) only if the consumer explicitly enables it.
5. **No JavaScript injected** — all data is captured server-side from `HttpContext`, Blazor circuit events, or app-level API calls.
6. **DNT respected by default** — when a visitor's `DNT` header is set, the web package skips telemetry entirely.

---

## Hashing Strategy

TelemetryForge uses three distinct hashes, each with a different salt strategy to prevent cross-referencing between data stores.

### 1. Session Hash (WebEvent.SessionHash)

**Purpose:** Groups individual web events into sessions.

**Input:** `session_id + IP address + "session:" + date`

**Salt:** Daily-rotating (`yyyy-MM-dd`), prefixed with `session:`

**Stored on:** `WebEvent` rows (used by the session materialization job)

**Lifecycle:** Ephemeral — events are materialized into `WebSession` rows (which do not carry the hash), then marked as processed. The hash on raw events is only used for grouping.

**Properties:**
- Same visitor + same session + same day = same hash (events group correctly)
- Same visitor + same session + next day = different hash (daily salt rotated)
- Same session ID reused from a different IP = different hash (IP is part of input)

### 2. Visitor Session Hash (VisitorHash.FirstSessionHash)

**Purpose:** Carries forward "New visitor" status across all events in the first session.

**Input:** `session_id + IP address`

**Salt:** None — this is an identity-scoped value that must match for the entire first session regardless of day boundaries.

**Stored on:** `VisitorHash` record (created on first visit)

**How it works:** When a visitor's first event arrives, the server inserts a `VisitorHash` record with `FirstSessionHash` set. Subsequent events in the same session produce the same unsalted hash, so `IsFirstSeenAsync` returns `true` (still "New"). Events from a different session produce a different hash, so the visitor is marked "Returning."

**Cannot be correlated with Session Hash** because:
- Session Hash includes `session:` prefix and daily salt
- Visitor Session Hash is unsalted with no prefix
- Same inputs produce completely different SHA-256 outputs

### 3. Visitor Identity Hash (VisitorHash.Hash)

**Purpose:** Determines whether a visitor has ever been seen before on a given site (first-visit detection).

**Input:** IP address or Google Analytics `_ga` cookie value

**Salt:** None — this is a permanent identifier for "have we seen this person before?"

**Stored on:** `VisitorHash` table, keyed by `(Hash, SiteId)`

**Lifecycle:** Permanent for the life of the data retention window. Never written to event or session tables.

---

## Data Flow

```
SDK sends: session_id, ip_address, ga_value, user_agent, page, ...
                │
                ▼
┌─────────────────────────────────────────────────────┐
│  TelemetryEndpoints.HandleWebPayload                │
│                                                     │
│  1. Hash session_id + IP + daily salt → SessionHash │
│     (stored on WebEvent for session grouping)       │
│                                                     │
│  2. Hash session_id + IP (no salt) → VisitorSession │
│     (compared against VisitorHash.FirstSessionHash) │
│                                                     │
│  3. Check VisitorHash table by IP/GA + SiteId       │
│     → New record? Insert + store VisitorSession     │
│     → Exists, same session? Still "New"             │
│     → Exists, different session? "Returning"        │
│                                                     │
│  4. Country/Region from payload (CloudFlare headers) │
│     If empty, fallback to GeoIP database on IP      │
│     (IP discarded after this step)                  │
│                                                     │
│  5. Parse User-Agent → Browser, OS, DeviceType      │
│                                                     │
│  6. Bot detection:                                  │
│     → UA matches known bot patterns? IsBot = true   │
│     → No Accept-Language header? IsBot = true       │
│                                                     │
│  7. Store enriched WebEvent (no raw IP, no raw      │
│     session_id — only hashed SessionHash)           │
└─────────────────────────────────────────────────────┘
```

---

## What Is Stored vs. What Is Discarded

### Stored (in WebEvent / WebSession)

| Field | Source | Notes |
|---|---|---|
| SessionHash | SHA-256(session_id + IP + daily salt) | Cannot be reversed to recover IP or session_id |
| IsFirstVisit | Derived from VisitorHash lookup | Boolean only |
| Page, StatusCode | Request data | No query strings or form data |
| Country, Region | SDK-provided (CloudFlare headers) or GeoIP database fallback | IP discarded after fallback lookup |
| Browser, OS, DeviceType | Parsed from User-Agent | Coarse values only (e.g., "Chrome 125", not the full UA string) |
| Referrer | HTTP Referer header | As sent by the browser |
| Language | Accept-Language header | First value only |
| EventType, EventName, EventData | Developer-defined | Custom events are opt-in |
| IsBot | Derived from UA + language check | Boolean flag |

### Discarded After Processing

| Field | Used For | Then |
|---|---|---|
| Raw IP address | Geolocation, hashing, rate limiting | Never persisted |
| Raw session_id | Hashing into SessionHash | Never persisted |
| Raw User-Agent string | Parsing browser/OS/device | Full string not stored (only parsed components) |
| Raw _ga cookie value | Visitor identity hashing | Never persisted |

### Stored on VisitorHash (Identity Table Only)

| Field | Purpose |
|---|---|
| Hash (IP or GA value) | First-visit detection — "have we seen this person?" |
| FirstSessionHash | First-visit carry-forward — "is this still their first session?" |
| FirstSeen | Timestamp of first visit |
| HashType, SourceType, SiteId | Classification metadata |

The VisitorHash table contains no behavioral data — no pages, no events, no sessions. It exists solely for identity resolution.

---

## Desktop / Mobile Identity

Desktop and mobile apps use different identity mechanisms:

| Platform | Identifier | Hashing |
|---|---|---|
| Desktop | Machine GUID (Windows), `/etc/machine-id` (Linux), IOPlatformUUID (macOS) | SHA-256 hashed client-side before transmission |
| Mobile | `identifierForVendor` (iOS), `ANDROID_ID` (Android), generated GUID (fallback) | SHA-256 hashed client-side before transmission |

The server never receives the raw machine or device identifier — only the pre-hashed value.

Desktop and mobile sessions use a `session_id` + `sequence` pattern for heartbeat-based updates. The `session_id` is a client-generated UUID per app session, stored directly (not hashed with a salt) because these are app-scoped sessions with no cross-site tracking concern.

---

## API Key Security

- API keys are generated using `RandomNumberGenerator` (cryptographically secure)
- Keys are bcrypt-hashed before storage — the raw key is shown once at creation, then discarded
- Keys are validated on every telemetry request via the `X-TelemetryForge-Key` header
- Rate limiting is partitioned by API key + client IP (30 requests/minute per visitor)

---

## Admin Authentication

- Local accounts use bcrypt-hashed passwords with account lockout after repeated failures
- Optional OIDC/SSO support (e.g., Microsoft Entra ID) — configured through the admin UI
- OIDC users must be pre-authorized by email before they can sign in
- Cookie authentication with `HttpOnly`, `Secure`, `SameSite=Strict` flags

---

## Bot Detection

Suspected bot traffic is flagged (not blocked) using two signals:

1. **User-Agent pattern matching** — known bot strings (Googlebot, crawlers, spiders, headless browsers)
2. **Missing Accept-Language header** — real browsers always send this; many bots don't

Flagged events are stored with `IsBot = true`. The dashboard excludes bot traffic by default, and the Event Stream provides a toggle to show/hide bots.

---

## Cross-Reference Protection Summary

| Table | Contains | Can be linked to |
|---|---|---|
| WebEvent | SessionHash (daily-salted) | Other WebEvents in the same session (same day only) |
| WebSession | Aggregated session data | Nothing — no hashes stored |
| VisitorHash | Visitor identity + FirstSessionHash (unsalted) | Nothing — different hash algorithm than WebEvent |
| DesktopSession | FingerprintHash, SessionId | Other heartbeats in the same app session |
| MobileSession | DeviceHash, SessionId | Other heartbeats in the same app session |

No single table can be joined to another to reconstruct a visitor's full browsing history across sessions.
