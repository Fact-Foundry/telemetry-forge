# Future Enhancements

Items listed here are planned but deferred from the initial implementation. Remove items once they are implemented.

## Database Providers

- **MySQL support** — via Pomelo.EntityFrameworkCore.MySql. Deferred until Pomelo ships a .NET 10-compatible package. PostgreSQL and SQL Server are available now.

## Database & Scaling

The server is the platform's heaviest and most *sustained* PostgreSQL consumer (continuous ingestion + Blazor analytics dashboard + the 2-min session materializer), all on a pool that shares the DB server's global `max_connections` budget with the other FactFoundry services. No connection leak today, but a few code-level items will matter as concurrent traffic grows:

- **Use `AddDbContextFactory`, not `AddDbContext`, for the Blazor Server UI** — `Program.cs` registers `TelemetryForgeDbContext` as scoped (`AddDbContext`). In Blazor Server a scope is the *circuit* lifetime (the whole interactive session), so a single long-lived context is shared across concurrent component renders/event handlers, risking the `"A second operation was started on this context"` error and keeping the change-tracker (and connection-prone context) alive far longer than needed. Switch to `AddDbContextFactory<TelemetryForgeDbContext>` and create a short-lived context per operation. Tightens connection usage and is the recommended EF Core pattern for Blazor Server. (The sibling FactFoundry Portal already uses the factory.)
- **Batch the per-session query in `SessionMaterializationService`** — `MaterializeSessions` fetches candidate `(SessionHash, SiteId)` keys, then issues **one query per candidate** in a `foreach` (N+1). For a backlog of N closed sessions that's N round-trips. Load all unmaterialized events for the candidate set in a single query (e.g. filter by the candidate keys, then group in memory) — or chunk it — so a materialization pass is a small fixed number of queries regardless of backlog size.
- **Pool sizing / pooler readiness** — per-pool `Maximum Pool Size` is set in the deployment env vars and draws from the shared `max_connections` ceiling. As real visitor concurrency climbs, this server needs a standing allocation of that budget, and the platform-level plan is a connection pooler (PgBouncer / Npgsql multiplexing). Tracked in the fact-foundry-platform design doc *DD-0002: Database connection pooling and scaling*.

## SDK Compatibility (telemetry-forge-sdk)

The **Web** (per-request `WebEventPayload`, ADR-003) and **Desktop** (heartbeat via `session_id` + `sequence`) packages are now implemented and shipped — verified against the live server (e.g. KevinOfTech.com runs `FactFoundry.TelemetryForge.Web` 1.1.3). The only remaining SDK gap is the Mobile package; see **Mobile Package — Heartbeat Support** below.

## Security Page

- **Disable bot scan button** — allow admins to toggle the "Scan for Bots" button on/off from the Security page (e.g., a setting to disable retroactive scanning when not needed)
- **Known IP exclusion** — allow admins to enter their own IP addresses in the portal. Traffic from known IPs is tagged as "Known" so it can be excluded from dashboard counts, analytics charts, and reports (e.g., filter out your own browsing while developing)

### Mobile Package — Heartbeat Support

Same heartbeat pattern as Desktop when eventually implemented:

- **Add `session_id` and `sequence` fields** to `MobileSessionPayload`
- **Implement heartbeat timer** with configurable interval
- **Add `device_hash_type` field** — "vendor_id", "android_id", or "generated_guid"

