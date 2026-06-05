# TelemetryForge — Security IP Logging Design

**Status:** Draft (for discussion)
**Date:** 2026-06-04
**Source:** Drafted via Claude (web), pasted for review.

> ### Notes for discussion (added on import — not part of the original draft)
>
> - **Overlaps [ADR-004](decisions/004-bot-and-security-event-classification.md).** ADR-004 already proposes a *tiered IP retention policy* (discard clean traffic immediately; short-TTL retain unknown/suspect) and an out-of-band *deep-scan service*. This doc is a more concrete, table-level design of that same idea. Reconcile the two before building — ideally fold the agreed parts back into ADR-004 or supersede that section with this.
> - **Table names need mapping.** This draft says `web_traffic` / `desktop_traffic`; the actual entities are `WebEvent` and `DesktopSession` (joined on `SessionHash`). The new `security_log` / `flagged_ips` tables are net-new.
> - **Relationship to the existing Ignored-IPs feature.** We already added an `IsIgnored` flag (admin-configured IPs excluded from analytics, raw IP never persisted). That is unrelated to this — it's an *exclude-my-own-traffic* control, not threat retention.
> - **Bot model is being reworked.** Per ADR-004, behavioral detections (`wp_scan`, `rapid_probe`, country-hop, etc.) are becoming "Security Events" with severity, distinct from verified bots. `flag_reason` values here should line up with that taxonomy.
> - **Privacy rule change.** CLAUDE.md currently states "Raw IP addresses are never persisted." This feature relaxes that to opt-in security retention; that hard rule needs updating if we proceed.

---

## Overview

This document describes the design for an opt-in IP address retention feature for security and threat detection purposes. This feature is intentionally separate from the existing analytics pipeline.

**Core principle:** IP addresses are never stored for behavioral or marketing analytics. When enabled by the server operator, IPs may be retained temporarily for security and threat detection purposes only.

---

## Motivation

TelemetryForge's existing bot detection flags suspicious traffic (WordPress scanners, distributed scrapers, vulnerability probes, etc.) but currently has no way to correlate flagged sessions back to specific IP addresses for threat analysis. This feature enables server operators to:

- Identify patterns across multiple requests from the same IP
- Build a persistent record of known malicious IPs
- Alert relevant parties (hosting provider, upstream security tools) with specific IP evidence

---

## Architecture

### Existing Tables (Unchanged)

- **web_traffic** — behavioral analytics for web sessions; never contains raw IPs
- **desktop_traffic** — behavioral analytics for desktop sessions; never contains raw IPs

These tables are not modified. Raw IPs never enter the analytics pipeline.

---

### New Tables

#### `security_log`

Stores raw IPs temporarily for active threat investigation. Subject to a rolling 30-day purge.

| Column | Type | Description |
|---|---|---|
| `id` | UUID / serial | Primary key |
| `session_hash` | varchar | Hashed session identifier — join key to `web_traffic` or `desktop_traffic` |
| `raw_ip` | varchar | Raw IP address (IPv4 or IPv6) |
| `flagged_at` | timestamp | UTC timestamp when the entry was created |
| `flag_reason` | varchar | Reason for logging (e.g. `bot_detected`, `wp_scan`, `rapid_probe`, `manual`) |

**Retention:** 30-day rolling purge via scheduled job. Entries older than 30 days are automatically deleted unless promoted to `flagged_ips`.

**Join pattern:** To investigate a flagged session, join `security_log.session_hash` to `web_traffic.session_hash` or `desktop_traffic.session_hash` to retrieve full behavioral context (user agent, path, country, browser, etc.) without duplicating that data here.

---

#### `flagged_ips`

Persistent record of confirmed malicious IPs. Survives the 30-day purge. Only IPs that have been actively reviewed and confirmed as malicious are promoted here.

| Column | Type | Description |
|---|---|---|
| `id` | UUID / serial | Primary key |
| `ip_address` | varchar | Raw IP address |
| `first_seen` | timestamp | UTC timestamp of first flagged activity |
| `last_seen` | timestamp | UTC timestamp of most recent flagged activity |
| `flag_reason` | varchar | Category of threat (e.g. `scanner`, `scraper`, `brute_force`) |
| `notes` | text | Optional free-text notes for investigation context |
| `created_at` | timestamp | UTC timestamp when this record was created |

**Purpose:** Long-term threat intelligence. Enables pattern recognition across multiple incidents and provides evidence for reporting to hosting providers or upstream security tools.

---

## Configuration

Security IP logging is **off by default**. Server operators opt in via a setting in the TelemetryForge server configuration.

When disabled (default):
- No IPs are written to `security_log` or `flagged_ips`
- Behavior is identical to current implementation
- Full privacy-by-architecture guarantee is preserved

When enabled:
- Bot detection events automatically write to `security_log`
- Manual flagging via the UI or API is available
- 30-day rolling purge runs on a configurable schedule

---

## Data Flow

```
Incoming request
    │
    ├─► IP used in-memory for geolocation lookup
    │       │
    │       └─► Country stored in web_traffic (no raw IP)
    │
    ├─► Bot detection runs
    │       │
    │       ├─► Not flagged: IP discarded (existing behavior)
    │       │
    │       └─► Flagged + security logging enabled:
    │               session_hash + raw_ip + flag_reason → security_log
    │
    └─► Session data (no raw IP) → web_traffic / desktop_traffic
```

---

## Purge Job

A scheduled background job handles the 30-day rolling purge of `security_log`.

**Recommended:** Run nightly or on a configurable cron schedule.

**Logic:**
```sql
DELETE FROM security_log
WHERE flagged_at < NOW() - INTERVAL '30 days';
```

Entries that have been reviewed and promoted to `flagged_ips` are unaffected — they live in a separate table and have no automatic purge.

---

## Privacy & Legal Considerations

- IP retention is for **threat analytics** only, not behavioral or marketing analytics
- Architectural separation between analytics tables and security tables makes intent unambiguous and auditable
- Opt-in by default ensures operators make a conscious decision to enable IP retention
- 30-day retention window is proportionate to the security purpose (GDPR Article 6(1)(f) — legitimate interests)
- Server operators who enable this feature should document it in their own privacy policy
- `flagged_ips` retains IPs indefinitely for confirmed malicious actors — this is defensible under legitimate interest given the security purpose

---

## UI Considerations (Future)

- Security log view in the TelemetryForge dashboard (separate from main event stream)
- Ability to promote a `security_log` entry to `flagged_ips` from the UI
- Ability to manually add IPs to `flagged_ips`
- Alert/notification when a known `flagged_ip` is seen again
- Export of `flagged_ips` for use in firewall rules or upstream reporting
