# Changelog

## [Unreleased]

### Features

- Initial project scaffold with Blazor Server + MudBlazor admin UI
- EF Core data layer with in-memory (dev) and PostgreSQL/MSSQL/MySQL (production) support
- Site/VisitorHash/AdminUser/ServerSetting entity models
- Telemetry ingestion pipeline — web, desktop, and mobile endpoints with API key validation, visitor hash resolution, event enrichment, and pub/sub publishing
- Payload DTOs for web, desktop, and mobile telemetry (WebPayload, DesktopPayload, MobilePayload, ErrorEvent)
- Enriched event models for downstream sinks (EnrichedWebEvent, EnrichedDesktopEvent, EnrichedMobileEvent)
- API key validation endpoint filter — validates X-TelemetryForge-Key header and resolves site ID
- Visitor hash resolution service — first-visit/first-install detection via hashed identifier lookup
- IEventPublisher interface with LoggingEventPublisher for development
- DatabaseEventPublisher — persists enriched events to WebSessions, DesktopSessions, MobileSessions tables
- CompositeEventPublisher — fans out events to multiple sinks (database + logging)
- Dashboard wired to real data — session counts (today/week/month), per-site breakdown, recent sessions with visitor/country columns
- Event Stream page — filterable feed of enriched events with expandable detail rows showing full session payload
- Session detail data stored in DB — feature paths, error events, page paths, and status codes persisted as JSON columns
- Site registration API endpoint stub
- Test project with xUnit — ApiKeyService, VisitorHashService, and DatabaseEventPublisher coverage
- Cookie authentication with bcrypt password hashing and account lockout
- API key generation using RandomNumberGenerator with bcrypt hashing
- Rate limiting on telemetry endpoints
- First-run wizard with admin account creation and server name setup
- Login page with email/password authentication
- Auth guards — unauthenticated users redirect to login, unconfigured instances redirect to setup wizard
- Logout endpoint
- Sites & Apps page — register sites, generate API keys (shown once), regenerate keys, delete sites with confirmation
- Dashboard page with session summary cards
- Navigation layout with dark mode on by default, light mode toggle
- Empty layout for setup and login pages (no sidebar)
- Sinks page — add, toggle, and delete HTTP event sinks with site scoping
- Settings page — server name, data retention, admin account management (add, delete, reset password)
- AuthService admin management — create/delete admins, reset passwords, server settings read/write
- AuthService test coverage — 11 tests covering create, delete, reset password, lockout, and settings CRUD
- User-Agent parsing — extracts browser, OS, and device type from web payloads via UAParser
- IP geolocation — resolves country/region from request IP using MaxMind GeoLite2, configurable via Settings page
- OIDC / SSO — OpenID Connect support for admin sign-in (e.g., Microsoft Entra ID), configurable via Settings page
- OIDC user authorization — admins must pre-authorize OIDC users by email before they can sign in
- Login page shows OIDC sign-in button when configured, with specific error messages for auth failures
- GeoIP and OIDC settings sections added to Settings page with active/not configured status indicators
- AddAdminDialog supports both local (password) and OIDC (email-only) account types
- 48 tests passing — added 8 OIDC auth tests, 8 UA parser tests, 4 GeoIP client IP extraction tests
- Per-request web telemetry (ADR-003) — web endpoint now accepts individual page events instead of end-of-session payloads
- WebEvent entity and WebEvents table — stores raw per-request events with event type (page_view, custom, link_click, circuit_close)
- Custom event support — developers can track arbitrary server-side events via event_type=custom with event_name and event_data
- Session materialization background job — groups WebEvents by session hash and materializes into WebSessions after configurable inactivity window
- Session inactivity window configurable from Settings page (default: 30 minutes)
- Dashboard "Active Now" card — shows distinct visitors in the last 5 minutes from WebEvents
- Rate limiter partitioned by API key + client IP (30/min per visitor) instead of per API key (100/min)
- Desktop/Mobile heartbeat support — session_id and sequence fields enable periodic partial updates instead of single end-of-session flush
- Desktop/Mobile upsert logic — heartbeats append feature path and error deltas to existing session rows
- Bot detection — flags suspected bot traffic via User-Agent pattern matching and missing Accept-Language header
- Dashboard excludes bot traffic from session counts and Active Now
- Event Stream "Hide Bots" toggle (on by default) with bot chip indicator
- 52 tests passing — added bot flag persistence test

### Docs

- README — added "What Gets Stored" section documenting stored fields for web, desktop, and mobile sessions
- Deployment guide — configuration, database setup, bare metal/Docker/systemd deployment, reverse proxy, GeoIP, and security notes
- ADR-003 — proposed move from end-of-session flush to per-request web telemetry for real-time visibility and cross-platform compatibility
- Future Enhancements — documented SDK compatibility work needed for per-request web events, heartbeat support, and custom events
