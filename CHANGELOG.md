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
- Site registration API endpoint stub
- Test project with xUnit — ApiKeyService and VisitorHashService coverage
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
