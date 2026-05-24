# Changelog

## [Unreleased]

### Features

- Initial project scaffold with Blazor Server + MudBlazor admin UI
- EF Core data layer with in-memory (dev) and PostgreSQL/MSSQL/MySQL (production) support
- Site/VisitorHash/AdminUser/ServerSetting entity models
- Telemetry ingestion API endpoint stubs (web, desktop, mobile)
- Site registration API endpoint stub
- Cookie authentication with bcrypt password hashing and account lockout
- API key generation using RandomNumberGenerator with bcrypt hashing
- Rate limiting on telemetry endpoints
- Dashboard page with session summary cards
- Navigation layout with dark mode toggle
