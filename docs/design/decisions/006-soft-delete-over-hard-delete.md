# ADR-006: Soft Delete Over Hard Delete

**Date:** 2026-06-28
**Status:** Proposed

## Decision

Split entities into two buckets:

- **Transactional data → hard delete** (governed by retention, no undo expected): `WebEvent`, the desktop/mobile session/event rows, `StoredErrorEvent`, `visitor_hashes`, and the future `ApiEvent` (ADR-005) and `HealthCheckResult` (ADR-007). These are deleted by retention jobs or by an explicit cascade (below).
- **Admin / configuration entities → soft delete**: `Site`, `Sink`, `AdminUser`, `DataApiKey`. A soft-deletable entity carries a deletion marker (a nullable `DeletedAt` timestamp, or an `IsDeleted` flag); "deleting" sets the marker, and all normal queries exclude marked rows.

**Site delete is a user choice.** The delete confirmation dialog offers **"Also delete all associated records"**:

- **Checked** → **hard-delete** the site *and* cascade-delete all of its transactional rows (events/sessions/errors/health results). Nothing left behind.
- **Unchecked** → **soft-delete** the site (set the marker) and keep its records. Not orphaned, because the parent `Site` row still exists — just hidden from normal queries and recoverable.

Either path guarantees **no orphaned records**, which is the whole point.

## Context

Two needs drive this: an admin who deletes a site by mistake should be able to recover it, and child transactional rows must never be left parentless when a parent is removed. The "also delete all associated records" option gives the deliberate, total purge when that is genuinely wanted.

The same reasoning applies project-wide for the admin/config bucket. Hard deletes there lose history and break referential context; soft deletes preserve integrity and allow undo/audit. Transactional data, by contrast, is high-volume and retention-governed — soft-deleting it would just be cost with no undo value, so it is hard-deleted.

## Consequences

- **Queries must filter** on the deletion marker, on the soft-delete bucket only. Prefer a single enforcement point — an EF Core global query filter (`HasQueryFilter`) per soft-deletable entity — so callers can't forget. Provide an explicit opt-out (`IgnoreQueryFilters`) for admin/recovery/purge views.
- **Uniqueness constraints** (e.g. site name, API-key hash) must account for soft-deleted rows. Decision: a **name** freed by a soft delete may be **reused**; a soft-deleted site's **API-key hash is excluded** from active key validation so the dead key cannot authenticate.
- **Schema cost.** Each soft-deletable entity gains a marker column; the cascade in the "also delete" path is a plain delete. Adding the marker column to an existing database is handled by an EF migration (ADR-008) — the old `EnsureCreated` gap (ADR-004's `BotName`) no longer applies.
- **Delete UI** still requires the confirmation dialog mandated by the coding standards; soft delete does not relax that. The dialog additionally carries the "also delete all associated records" choice for `Site`.
- **Genuine purge** (retention / GDPR erasure / the "also delete" cascade) remains available as an explicit, separate operation distinct from the everyday soft delete.

## References

- ADR-005 — per-site config lives as typed `Site` columns; the wrong-type re-registration flow relies on the "also delete all associated records" option here.
- ADR-007 — `HealthCheckResult` is transactional (hard delete / retention); a soft-deleted site stops being polled.
- ADR-008 — EF Core migrations applied at startup, which add the soft-delete marker columns to existing databases.
