# ADR-008: Database Schema Migrations (EF Core Migrations Applied at Startup)

**Date:** 2026-06-28
**Status:** Accepted

## Decision

Use **EF Core Migrations** as the single schema mechanism. Migrations are authored at development time from model changes (`dotnet ef migrations add`) and applied **automatically at runtime** via `Database.Migrate()` on startup, for the relational providers (PostgreSQL, SQL Server). The in-memory provider (dev/test) continues to use `EnsureCreated`, which does not support migrations. Databases originally created by `EnsureCreated` are **baselined automatically** on first run, so the transition needs no manual steps.

This is the prerequisite (**Phase 0**) for ADR-005, ADR-006, and ADR-007, all of which add schema.

## Context

- Schema was built by `EnsureCreated` (dev-only), and production was patched with ad-hoc idempotent SQL scripts generated on the fly and **not reliably version-controlled**. For an open-source product that other people deploy, that is too fragile: a missed or wrong script can break a self-hoster's upgrade.
- Requirement (stated): a mechanism that is **reliable, hard to bork, version-controlled, and zero-manual-step for self-hosters** on upgrade.

## Why EF Migrations (and not a hand-rolled reconciler)

- An earlier draft of this ADR chose a custom startup reconciler that inspects `information_schema` and issues `CREATE TABLE` / `ADD COLUMN`. That reconciler is **hand-maintained** — its expected-schema list and per-provider DDL can drift from the EF model, which is exactly the subtle breakage we want to avoid.
- EF Migrations are **generated from the model** (the single source of truth), ordered, tracked in `__EFMigrationsHistory`, idempotent, additive by default, and battle-tested. Each migration is reviewed at dev time and committed to source control — the "saved, versioned script" that was missing.
- The only prior objection — *"dotnet isn't installed on the server"* — does **not** apply: `Database.Migrate()` runs from the bundled, self-contained app at runtime. The `dotnet ef` CLI is only needed on the *developer's* machine to author migrations.

## Transition from EnsureCreated (one-time, automated)

On startup, for relational providers:

1. `__EFMigrationsHistory` absent **and** core tables already exist (e.g. `Sites`) → legacy `EnsureCreated` database. Create the history table and record the initial baseline migration as already applied — **no DDL is run against existing objects**.
2. `__EFMigrationsHistory` absent and no tables exist → fresh database. `Migrate()` creates everything and records history.
3. Otherwise → normal `Migrate()` applies only the pending migrations.

All three paths are non-destructive and require no operator action.

## Decision detail / consequences

- An **Initial** migration is generated representing the current schema (what `EnsureCreated` produced). Every subsequent change is a new migration.
- Startup calls `Database.Migrate()` for relational providers; `EnsureCreated` remains for the in-memory provider only.
- Self-hoster upgrade = deploy the new app version; it migrates itself. No SQL to run by hand.
- For operators who want a manual review gate, EF can still emit a script (`dotnet ef migrations script --idempotent`) to apply out-of-band; runtime auto-migrate stays the default.
- Destructive changes (drops/renames) still require deliberate authoring and review **in the migration** — EF surfaces them rather than hiding them.
- Tests / CI: migrate-from-empty and baseline-from-legacy paths on a relational provider; a CI guard that there are no un-migrated model changes (`dotnet ef migrations has-pending-model-changes`).

## References

- ADR-004 — the `BotName` column that first exposed the schema-update gap.
- ADR-005 / ADR-006 / ADR-007 — the schema-adding ADRs that depend on this Phase 0.
