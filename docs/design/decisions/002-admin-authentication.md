# ADR-002: Admin UI Authentication

**Date:** 2026-05-24
**Status:** Accepted

## Decision

Use a dual authentication approach without ASP.NET Identity:

1. **Local password auth** — admin credentials (email + bcrypt-hashed password) stored in the application database, using ASP.NET cookie authentication with standard `ClaimsPrincipal` integration.
2. **OpenID Connect (OIDC)** — optional external provider support (e.g., Microsoft Entra ID) via ASP.NET's built-in `AddOpenIdConnect()` middleware. Configured entirely through the admin UI Settings page and stored in the database.

Both methods share the same cookie authentication scheme. The login page offers local sign-in and, when OIDC is configured, an external sign-in button.

## Context

ASP.NET Identity brings a full user management framework with roles, claims, two-factor auth, account confirmation, password reset tokens, and a set of database tables. For TelemetryForge's admin UI — which serves a small number of administrators on a self-hosted instance — this is more infrastructure than needed.

However, the server may be exposed to the internet, so the implementation must still be secure:

- Passwords hashed with bcrypt (same library already used for API keys)
- Cookie authentication with `HttpOnly`, `Secure`, and `SameSite=Strict` flags
- Account lockout after repeated failed login attempts
- HTTPS enforcement on public deployments

OIDC support allows administrators to sign in with their existing organizational identity (e.g., Entra ID) without requiring ASP.NET Identity. The `AddOpenIdConnect()` middleware handles the full OIDC flow independently — token exchange, claims mapping, and session management are all built into the framework.

### OIDC Configuration

OIDC settings are managed entirely through the admin UI Settings page — no config files to edit. The settings are stored in the database:

- **Enabled** — toggle on/off
- **Authority URL** — e.g., `https://login.microsoftonline.com/{tenant-id}/v2.0`
- **Client ID** — from the Entra ID app registration
- **Client Secret** — encrypted at rest in the database
- **Display Name** — button label on the login page (e.g., "Sign in with Microsoft")

On startup, the server loads OIDC settings from the database. When an admin saves new OIDC settings, the authentication configuration is updated dynamically without requiring a server restart. When OIDC is disabled or not yet configured, the login page shows only local password auth.

### Authorization for OIDC Users

On first OIDC sign-in, the user is not automatically an admin. An existing admin must authorize the OIDC user's email in the admin UI before they can access the application. This prevents any Entra ID user in the tenant from gaining admin access simply by authenticating.

## Consequences

- Simpler database schema — an `AdminUsers` table rather than the 7+ tables ASP.NET Identity creates
- OIDC users are also tracked in `AdminUsers` (linked by email, no password hash)
- No built-in password reset flow — admin resets are handled via a CLI command or another admin
- Single OIDC provider supported initially — multiple providers could be added later if needed
- No dependency on ASP.NET Identity — the OIDC middleware is part of the base ASP.NET framework
