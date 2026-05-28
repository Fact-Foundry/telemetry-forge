# Changelog

## [1.1.3]

### Features

- Country-hop bot detection — sessions appearing from 3+ distinct countries are flagged as bot, with retroactive flagging of prior events in the session
- Data API — read-only REST endpoints (`/api/data/web-events`, `/api/data/desktop-sessions`, `/api/data/mobile-sessions`) with pagination, date range, site, bot, and event type filters
- Data API keys — site-scoped API keys (`tfrg_data_` prefix) for granting read access to specific site groups (e.g. separate keys for production vs sandbox)
- DataApiKey entity with BCrypt-hashed keys and JSON site ID list
- Display timezone setting (General) — configures timezone for all dates in the admin UI
- API response timezone setting (API) — configures timezone for date values in Data API responses, independent of the display timezone

### Fixes

- Web timestamp field changed from DateTime to DateTimeOffset — fixes 400 rejection of SDK payloads with fractional-second ISO 8601 timestamps
- Fixed page_path field name mismatch — server expected "page" but SDK sends "page_path", causing empty Page on all web events

### UI Improvements

- Analytics page — line charts for sessions by page, country, and referrer over selectable periods (Today, 7 Days, 30 Days) with site filter
- Domain field on Sites — used to filter self-referrals from the referrer chart (e.g. kevinoftech.com won't appear as a referrer for its own site)
- Added Country column to Event Stream table
- Dashboard Sites & Apps table — period dropdown (Today, This Week, Last 30 Days, Last 90 Days, Last Year) with Sessions, Bots, and Total columns
- Added `circuit_open` event type to Event Stream color mapping
- Added Session Hash column to Event Stream table (truncated) with full hash in expanded detail panel
- Event Stream now shows 100 events (up from 50) with denser layout
- Event Stream CSV export button — downloads all visible events as `telemetry-events.csv`
- Dashboard site names link to Event Stream pre-filtered by that site
- Dashboard period dropdown made more compact (Dense margin, narrower width)
- Settings restructured into sub-pages — General (server, GeoIP, session, retention), Authentication (OIDC, admin accounts), API (data key management)
- Settings nav item is now an expandable group with sub-page links
- Event Stream table uses fixed column widths with ellipsis truncation for long page paths
- Event Stream detail panel wraps long URLs instead of overflowing
- Event Stream time column uses compact date format (`M/d/yyyy H:mm`)
- Event Stream table cell padding reduced for denser rows

## [1.1.0]

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
- Removed LicenseJwt/LicenseTier from desktop telemetry pipeline — payload, enrichment, storage, and UI
- Session identity via hashed session_id + IP + daily salt — prevents cross-day tracking from reused session IDs
- First-visit carry-forward via FirstSessionHash on VisitorHash — all events in the initial session show "New"
- Cross-reference protection — session hash uses different salt strategy than visitor hash, preventing table joins
- Two-tier geolocation — SDK reads CloudFlare headers (CF-IPCountry, CF-Region) as primary source, server falls back to MaxMind GeoIP database
- Added country and region fields to web payload for SDK-provided geolocation
- Removed SessionHash from materialized WebSession entity (privacy — no trackable data in session records)
- Renamed payload fields: ip_hash → ip_address, ga_hash → ga_value (server does the hashing, not the SDK)
- Client Hints support — sec_ch_ua, sec_ch_ua_mobile, sec_ch_ua_platform fields on web payload for accurate browser identification (e.g., Brave vs Chrome)
- 57 tests passing — added Client Hints parser tests

### Docs

- README — added "What Gets Stored" section documenting stored fields for web, desktop, and mobile sessions
- Deployment guide — configuration, database setup, bare metal/Docker/systemd deployment, reverse proxy, GeoIP, and security notes
- ADR-003 — proposed move from end-of-session flush to per-request web telemetry for real-time visibility and cross-platform compatibility
- Security & Privacy doc — hashing strategy, data flow, stored vs. discarded fields, cross-reference protection
- Deployment guide updated with two-tier geolocation approach (CloudFlare primary, GeoIP fallback)
- Architecture spec updated — three-hash identity model, per-request payload schema, removed license references
- Future Enhancements — documented SDK compatibility work needed for per-request web events, heartbeat support, and custom events
