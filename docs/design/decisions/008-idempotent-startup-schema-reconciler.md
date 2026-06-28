# ADR-008: Idempotent Startup Schema Reconciler

**Date:** 2026-06-28
**Status:** Accepted

## Decision

After `EnsureCreated`, run an **idempotent startup schema reconciler** that brings an existing database up to the current expected schema — **creating missing tables and adding missing columns** — instead of adopting EF Core migrations. This is the single, project-wide mechanism for every future schema addition. It is the prerequisite (**Phase 0**) for ADR-005, ADR-006, and ADR-007, all of which add schema.

## Context

- The project rides `EnsureCreated` with no migrations. `EnsureCreated` builds the full schema **only when the database does not yet exist**; on an existing database it does **nothing** — it never adds a new table or column. So every schema addition (`BotName` in ADR-004, and the new tables/columns in ADR-005/006/007) silently fails to appear on a self-hoster's existing database on upgrade.
- We had been papering over this with hand-written idempotent `ALTER`s against prod — error-prone and easy to forget. Each new ADR independently re-flagged "the `EnsureCreated` gap"; this ADR resolves it once.

## Why not EF migrations

EF migrations are genuinely liked, but they don't fit this deployment:

- Migrations **can** run at runtime via `Database.Migrate()` even in a self-contained, no-dotnet-CLI bundle (e.g. the web server where the .NET runtime isn't installed and ships bundled with the app) — so that part isn't the blocker.
- The blocker is the **existing data**: databases built by `EnsureCreated` have no `__EFMigrationsHistory` table, so converting each live deployment to migrations is a painful one-time baseline-and-reconcile per database.
- A reconciler matches what we already do by hand, avoids that conversion, and keeps the zero-friction "no manual migrations" promise in the spec.

## Decision detail

- On startup, **after** `EnsureCreated`, the reconciler inspects the live schema (provider metadata / `information_schema`) and applies the missing `CREATE TABLE` / `ADD COLUMN` statements idempotently. Provider-aware (PostgreSQL / MSSQL).
- This becomes the **single home for every future schema change**. New entities and columns are registered with the reconciler rather than left to `EnsureCreated`.
- **Additive only by design** — it creates tables and adds columns. It does **not** drop or retype existing columns; genuinely destructive changes remain a deliberate, manual operation.

## Consequences

- Every ADR that adds schema routes through the reconciler: ADR-005's `ApiEvent` and per-site config columns on `Site`, ADR-006's soft-delete markers, ADR-007's `HealthCheckResult`. The "`EnsureCreated` gap" caveat in those ADRs is resolved here and replaced by a reference to this ADR.
- Because config now lives in **typed columns** (added safely by the reconciler), per-site configuration no longer needs the generic `ServerSetting` KV table — see the revised conventions in ADR-005.
- Additive-only means renames / type changes still need deliberate, separate handling.
- Requires per-provider schema-inspection coverage and tests (missing-table and missing-column paths).

## References

- ADR-004 — the `BotName` column that first exposed the gap.
- ADR-005 / ADR-006 / ADR-007 — the schema-adding ADRs that depend on this Phase 0.
