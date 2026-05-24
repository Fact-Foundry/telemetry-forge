# Claude Code Instructions for TelemetryForge Server

## Project Overview

The central server for TelemetryForge — an ASP.NET Minimal API with a built-in Blazor admin UI that receives telemetry payloads from client packages, resolves visitor identity, enriches records, and forwards events to configured downstream sinks. Licensed under AGPL-3.0.

The client NuGet packages (Web, Desktop, Mobile) are in a separate repository (`telemetry-forge-sdk`, MIT).

## First Steps

**Read `docs/design/telemetryforge-spec.md` at the start of every session** — it contains the full system design including payload schemas and identity resolution. Subsequent decisions will be in documents in the `docs/design/decisions/` folder.

## Architecture

- .NET 10, ASP.NET Minimal API, Blazor Server admin UI
- MudBlazor UI framework (admin UI)
- EF Core — in-memory provider for development/testing, PostgreSQL/MSSQL/MySQL for production
- Lightweight custom auth with optional OIDC (see ADR-002)

## Project Structure

| Project | Purpose |
|---|---|
| `FactFoundry.TelemetryForge.Server` | Central ASP.NET Minimal API + Blazor admin UI |

## Build Commands

- **Build:** `dotnet build src/FactFoundry.TelemetryForge.Server/FactFoundry.TelemetryForge.Server.csproj`
- **Solution:** `dotnet build TelemetryForge.sln`
- **Tests:** `dotnet test`

## API Endpoints (receives payloads from client packages)

| Endpoint | Source | Description |
|---|---|---|
| `POST /api/telemetry/web` | Web package | Web session payloads |
| `POST /api/telemetry/desktop` | Desktop package | Desktop session payloads |
| `POST /api/telemetry/mobile` | Mobile package | Mobile session payloads |
| `POST /api/sites/register` | Admin UI | Register a new site/app, receive API key |

## Coding Standards

- MudBlazor for all admin UI components
- **XML comments required on all public APIs** — all public classes, methods, and properties must have XML doc comments (`/// <summary>`)
- Use compact, clean UI layouts — avoid excessive padding/spacing
- **Delete actions require confirmation** — any delete button or menu item must show a confirmation dialog before performing the deletion to prevent accidental data loss
- API keys must be generated using `RandomNumberGenerator` — never `Random`, never `Guid.NewGuid()`
- API keys are bcrypt hashed before storage — the raw value is never persisted
- Raw IP addresses are never persisted — geolocate on ingestion then discard
- All catch blocks should log meaningful error context — never swallow exceptions silently

## Workflow Rules

- **Do not commit, push, or tag** unless explicitly asked
- **Do not create markdown files** for planning/tracking in the repo
- **Deferred features** go in `docs/Future Enhancements.md` — remove items once they are implemented
- **Log all changes in `CHANGELOG.md`** under the current unreleased version. Group entries under Features, Fixes, UI Improvements, or Docs. Keep entries concise (one line each)

## Testing Rules

- **Run tests after every change** — build and run `dotnet test` before reporting a change as complete
- **Never silently fix a failing test** — if a code change breaks or invalidates an existing test, STOP and flag it. The test exists because that behavior was intentional. Ask whether the behavior change is correct before modifying the test to pass
- **Add tests for new logic with branches** — error mapping, status transitions, fallback paths, and computed values all need test coverage. If it has an `if`, it probably needs a test
- **Update tests when contracts change** — if a method's return type, exception behavior, or public API changes, update the corresponding tests to reflect the new contract and explain why the old behavior is no longer correct

## Implementation Approval Workflow

For any non-trivial change, follow this sequence — do not skip to writing code:

1. **Read** all relevant files first
2. **Restate** your interpretation of the requirement
3. **Propose** your implementation plan
4. **Wait for explicit approval** before writing any code
5. **Implement** once approved

**Skip the workflow** for simple, clearly scoped tasks (typo, single CSS fix, rename).
