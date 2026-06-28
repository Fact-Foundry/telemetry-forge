# ADR-007: Active Health-Check / Uptime Monitoring

**Date:** 2026-06-28
**Status:** Proposed

## Decision

Add **active synthetic monitoring**: a background service in TelemetryForge that periodically calls a registered site's health URL, records each poll result, and **alerts** when a site goes down (and recovers). This is the complementary half of API health to ADR-005's passive ingestion — passive tells you *what happened when traffic arrived*; active tells you *it's down even when no one is calling*.

1. **A hosted background service** polls each enabled site's configured health URL on its interval (pattern: existing `SessionMaterializationService`).
2. **Per-site config via nullable typed columns on `Site`** (the ADR-005 convention) — health URL, interval, timeout, expected status, enabled flag, optional (encrypted) auth header — each with a **global default cached in memory** when the site's column is null. The reconciler (ADR-008) adds the columns.
3. **A new `HealthCheckResult` entity/table** stores each poll outcome; the reconciler (ADR-008) creates it on existing databases. `HealthCheckResult` is **transactional data — hard-deleted / retention-governed** (ADR-006), never soft-deleted.
4. **SSRF guardrails** — the operator supplies the URL, so the server makes outbound calls to an arbitrary address. Block loopback/private/link-local/cloud-metadata by default.
5. **Self-probe exclusion** — tag the probe so an instrumented target's own SDK doesn't record TF's health calls as telemetry.
6. **Alerting** — notify via the ADR-004 sinks pipeline after **N consecutive failures** (anti-flap), plus a **recovery** notice.

## Context

- ADR-005 makes API a first-class telemetry type, but ingestion is **passive** — TF only knows about an API when the API (or its callers) send something. A *down* API and a *quiet* API look identical: no events. Active polling closes that gap.
- The user wants "a heartbeat checker from the TelemetryForge side. Every so often it can call the `/health` endpoint on an API and check its status in case the API is down" — plus alerting.
- This is **synthetic monitoring** (TF originates traffic), categorically different from every existing feature, which is passive ingestion. It is **not API-only**: any site with a reachable health URL (Web included) can be monitored.

## Active vs. passive (why both)

| | Passive (ADR-003/005) | Active (this ADR) |
|---|---|---|
| Traffic origin | the app / its callers | TelemetryForge |
| Answers | "what happened when called" | "is it up right now" |
| Blind spot | can't tell *down* from *quiet* | doesn't see real usage |

They are complementary; neither replaces the other.

## Self-probe noise

If TF polls an instrumented API's `/health`, the API's own SDK would report each poll back as a request — TF's monitoring would inflate the very telemetry it sits beside.

- **Detection itself does not depend on this** — TF records the poll result directly (status/latency/reachability of *its own* outbound call); it never waits for the target to report back. So uptime is correct regardless.
- The noise is only in the target's *ingested* telemetry. Fix: TF sends a known marker on every probe — header `X-TelemetryForge-HealthCheck: 1` (and/or a recognizable User-Agent) — and the SDK middleware **skips telemetry for marked requests**. Documented SDK behavior; self-hosters with a custom health endpoint can also just exclude the route.

## SSRF guardrails

The health URL is operator-supplied, so the server issues outbound HTTP to an arbitrary destination — classic SSRF surface.

- **Block by default:** loopback (`127.0.0.0/8`, `::1`), private (`10/8`, `172.16/12`, `192.168/16`, `fc00::/7`), link-local (`169.254/16` — includes cloud metadata `169.254.169.254`), and other non-routable ranges.
- Resolve the hostname and check the **resolved IP**, not just the literal, to defeat DNS rebinding to a blocked range.
- **Allowlist override** for operators who legitimately monitor an internal service (the TF box itself reaches private hosts on `10.0.0.x`). Stored the same way as other config — a nullable `Site` column (or a cached global default), per ADR-005.
- Cap redirects, timeout, and response size; only GET; never echo the response body into storage (status + latency + reachability only — telemetry, not logging, consistent with ADR-005).

## Alerting

- **Channel:** reuse the ADR-004 sinks pipeline (email / webhook / Slack) rather than inventing a notifier.
- **Anti-flap:** alert only after **N consecutive failures** (configurable, default e.g. 3), not on a single blip.
- **Recovery:** send a recovery notice when a site transitions back to healthy, so an alert always has a matching all-clear.
- **State, not spam:** alert on *transitions* (healthy→down, down→healthy), not on every failed poll.

## Data model

`HealthCheckResult` (transactional, retention-governed): site id, checked-at timestamp, reachable (bool), status code (nullable), latency ms, failure reason (enum: timeout / connection-refused / dns / unexpected-status / blocked-by-ssrf-guard). Coarse and bounded — no response bodies, no headers.

Per-site config — nullable columns on `Site` (global default cached in memory when null), per ADR-005:
- `HealthCheckUrl`, `HealthCheckInterval`, `HealthCheckTimeout`, `HealthCheckExpectedStatus`, `HealthCheckEnabled`, `HealthCheckAuthHeader` (encrypted at rest).
- Global defaults (interval, timeout, expected status) live in `appsettings` / a single `ServerSetting`, cached and refreshed on save — the `IpFilterService` cache pattern.

## Dashboard

An **uptime view**: current up/down per site, uptime % over a window, latency trend, and an incident list (down→recovery spans with duration). Distinct from both the Web sessions view and the ADR-005 API-health view; complements them.

## Schema impact

| Piece | Mechanism | Schema change? |
|---|---|---|
| `HealthCheckResult` storage | new entity/table | **Yes** — created by the reconciler (ADR-008) |
| Per-site health-check config | nullable `Site` columns | **Yes** — added by the reconciler (ADR-008) |
| Global config defaults | `appsettings` / single `ServerSetting`, cached | None |

## Phasing

1. **Phase 1** — hosted poller + per-site config + `HealthCheckResult` + SSRF guardrails + self-probe marker. Records uptime; no alerts yet.
2. **Phase 2** — uptime dashboard (status, uptime %, latency, incidents).
3. **Phase 3** — alerting via sinks (N-consecutive-failure threshold + recovery notice) + admin UI for channels/thresholds.

## References

- ADR-003 — passive per-request web events (the passive counterpart).
- ADR-004 — sinks/notification pipeline (reused for alerting).
- ADR-005 — first-class API health (passive half); per-site config convention (nullable `Site` columns + cached global default) reused here.
- ADR-006 — soft delete (a soft-deleted site stops being polled); `HealthCheckResult` is transactional / hard-deleted.
- ADR-008 — idempotent startup schema reconciler (creates `HealthCheckResult` and adds the per-site config columns).
- `Services/SessionMaterializationService.cs` — the hosted background-service pattern this follows.
- `Services/IpFilterService.cs` / `CidrRange` — reused for the SSRF range checks and the cached-config pattern.
