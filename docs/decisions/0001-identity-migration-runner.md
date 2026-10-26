# Identity schema changes run in a separate executable

Date: 2026-10-25. Status: accepted for the Identity migration setup.

## Context

Identity has PostgreSQL mappings and a registration API but needs a repeatable
schema setup. The supplied BookingSystem Cinema runner demonstrates a one-shot
generic host and Docker startup dependencies. Identity's API registration also
requires Redis and security options that are unrelated to schema changes.

## Decision

Keep EF migrations and the design-time factory in Identity Infrastructure. Run
them through `DevFlow.Identity.MigrationRunner`, referencing its own Infrastructure
and BuildingBlocks only. Register persistence directly, propagate shutdown
cancellation, and exit unsuccessfully when migration fails. Do not seed data.

Use a separate Identity Compose stack with PostgreSQL 18, a persistent database
volume, internal Redis, a one-shot migration service, and the API. Database health
gates migrations; successful completion and Redis health gate API startup. Keep
the existing Tasks stack and default Docker target intact.

## Consequences

APIs never perform DDL on startup. Runtime and migration credentials can be
separated in production. A failed initial migration prevents a fresh API rollout.
Deployments must explicitly complete the migration job before replacing running
API containers. The local owner credential is a development convenience.

The initial migration includes the three expression indexes required by existing
mappings. Tenant RLS/session state, append-only grants, retention, seed catalogs,
and production role provisioning remain separate work. See
[migration operations](../identity/MIGRATIONS.md).
