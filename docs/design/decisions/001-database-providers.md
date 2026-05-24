# ADR-001: Database Provider Strategy

**Date:** 2026-05-24
**Status:** Accepted

## Decision

Use EF Core with an in-memory provider for local development and testing. Wire up PostgreSQL, MSSQL, and MySQL as optional production providers, selectable via configuration.

## Context

The original spec mentioned SQLite as the default with Postgres/SQL Server as options. After discussion, the preference is:

- **Development/Testing:** EF Core in-memory provider — zero setup, fast test execution, no file artifacts
- **Production:** PostgreSQL, MSSQL, or MySQL — configured via connection string and a provider selector in settings

SQLite is dropped as an option. The in-memory provider covers the "zero config" local scenario, and production deployments should use a real database server.

## Consequences

- The data layer uses EF Core with `DbContext` abstractions that are provider-agnostic
- Provider registration happens at startup based on configuration
- Database migrations are managed per-provider where needed
- The first-run wizard offers a choice of PostgreSQL, MSSQL, or MySQL with connection string input
- Tests use the in-memory provider exclusively — no external database dependency for CI
