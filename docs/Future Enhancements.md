# Future Enhancements

Items listed here are planned but deferred from the initial implementation. Remove items once they are implemented.

## Database Providers

- **MySQL support** — via Pomelo.EntityFrameworkCore.MySql. Deferred until Pomelo ships a .NET 10-compatible package. PostgreSQL and SQL Server are available now.

## SDK Compatibility (telemetry-forge-sdk)

The SDK packages need updates to match the server's current ingestion API. The server moved from session-level payloads to per-request web events (ADR-003) and added heartbeat support for desktop/mobile.

### Web Package — Per-Request Events

The server now expects `WebEventPayload` (one event per request) instead of `WebSessionPayload` (one payload per session). The SDK's `TelemetryForgeMiddleware` and `TelemetryForgeCircuitHandler` need to be rewritten:

- **Replace `WebSessionPayload` with `WebEventPayload`** — fields: `ip_address`, `ga_value`, `session_id`, `user_agent`, `referrer`, `language`, `page`, `status_code`, `event_type`, `event_name`, `event_data`, `target_url`, `country`, `region`, `timestamp`, `dnt`
- **Middleware**: post one event per request (event_type=page_view) instead of accumulating a session. Send raw IP — server does the hashing. Read CloudFlare headers (CF-IPCountry, CF-Region) if available and include as `country`/`region`
- **Circuit handler**: post a page_view event on each `TrackNavigation()` call instead of accumulating and flushing at circuit close. Optionally send a circuit_close event when the circuit ends (for last-page duration calculation)
- **Custom event API**: add `ITelemetryForge.TrackEvent(string eventName, Dictionary<string, object>? eventData)` so developers can fire server-side custom events (event_type=custom)
- **Link click tracking (Blazor only, opt-in)**: add JS interop to capture anchor clicks and send link_click events with target_url. Acceptable because Blazor already requires JS

### Desktop Package — Heartbeat Support

The server now supports session upsert via `session_id` + `sequence` fields, allowing periodic partial updates instead of a single end-of-session flush:

- **Add `session_id` field** — client-generated UUID, stable for the app session lifetime
- **Add `sequence` field** — monotonically increasing counter (0 for first heartbeat)
- **Implement heartbeat timer** — periodically flush deltas (new features, new errors) on a configurable interval (e.g., every 15-30 minutes). Send only new feature_path entries and error_events since the last heartbeat, not the full accumulated list
- **Keep end-of-session flush** — final flush on dispose with sequence=N and full duration
- **Configuration**: add `HeartbeatIntervalMinutes` to `DesktopTelemetryOptions` (configurable, disabled by default for backward compatibility)

### Mobile Package — Heartbeat Support

Same heartbeat pattern as Desktop when eventually implemented:

- **Add `session_id` and `sequence` fields** to `MobileSessionPayload`
- **Implement heartbeat timer** with configurable interval
- **Add `device_hash_type` field** — "vendor_id", "android_id", or "generated_guid"

