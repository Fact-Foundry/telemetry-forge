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
