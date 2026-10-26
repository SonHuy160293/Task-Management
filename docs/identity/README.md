# DevFlow Identity & Access - execution documentation

**Status:** endpoint planning package with implementation notes linked below. **Last reviewed:** 2026-10-25.

Current database setup: [Migrations and runner](MIGRATIONS.md). Identity now has an
initial PostgreSQL 18 migration for the eighteen mapped tables and three expression
indexes, plus a dedicated Docker stack. It adds no seed data. RLS, runtime-role
restrictions, and append-only audit grants remain separate work. The original
model-only plan and files under `reference/` retain their historical task scope.

This package covers a multi-tenant B2B software-delivery product with project, task, team, risk and performance management. It expands the existing **18-table Identity ERD** into independently executable **single-endpoint CQRS tasks** for Codex. Every task begins **Pending**.

## Start here

1. [Research and user stories](RESEARCH_AND_SCOPE.md) - who uses Identity, what they need, capability decisions, source references and deferred gaps.
2. [Execution guide](EXECUTION_GUIDE.md) - exact one-task-per-Codex-run procedure, CodeGraph and `add-cqrs-api-feature`, exit gates and status updates.
3. [Backlog](BACKLOG.md) - 51 routes/tasks with `Pending`/`Completed` status.
4. [API conventions](API_CONVENTIONS.md) - authentication, HTTP semantics, PostgreSQL RLS, role and scope invariants.
5. [Permission catalog](PERMISSION_CATALOG.md) - proposed **application seed requirements** for the existing global role and permission tables.
6. [Codex prompt](CODEX_PROMPT.md) - reusable prompt for implementing just one task.
7. [Authoritative ERD/DDL/model plan](reference/) - copies of existing user-provided source documents, **not revised database migrations**.

## Phases and ownership

| Phase | Backlog IDs | Endpoints | Goal |
|---|---|---:|---|
| A - Authentication | ID-001..ID-014 | 14 | Accounts, verification, login, rotation, recovery, profile and sessions |
| B - Organizations | ID-015..ID-023 + ID-051 | 10 | Workspace bootstrapping, lookup, membership lifecycle |
| C - Invitations | ID-024..ID-029 | 6 | Invitations, preview, acceptance and resend |
| D - Access control | ID-030..ID-039 | 10 | Roles, permission catalog, assignments, extra grants, effective access |
| E - Platform and audit | ID-040..ID-050 | 11 | Tenant/platform security audit and platform operations |
| **Total** | | **51** | **28 P0 + 23 P1** |

P0 = essential B2B MVP flows. P1 = self-service and operational refinements. P1 tasks are not automatically safe to defer if an endpoint relies on their prerequisites; see `Dependencies` on each card.

## Authoritative schema rule

- Global definitions: `shared` schema. Tenant-owned records: `dbo` schema plus `TenantId`.
- Global User belongs to multiple organizations via `dbo.OrganizationMembers`.
- Permission = system-defined role permissions UNION direct member ALLOW grants at the **exact scope**, gated by account, tenant, membership, role/permission and resource state.
- **Database identifier casing:** Follow the previous model implementation specification (`shared.Users`, `dbo.MemberPermissionGrants`, `PrimaryEmail`) when generating EF migrations. The original SQL reference uses snake_case; it documents types and constraints, **not final PascalCase physical spelling**.
- Neither `AccessScope.ResourceId` nor identity entities own actual Projects, Teams, Tasks or Performance Reviews; resource existence, membership and visibility are checked by their owning modules.

## Sources and scope caveat

The reference ERD, SQL and uploaded `add-cqrs-api-feature` skill are user-supplied design inputs. Research-based controls are linked in `RESEARCH_AND_SCOPE.md`. Endpoint task contracts describe proposed behavior until verified against source and acceptance checks. The original planning package was prepared separately from the source tree; current repository code and agent instructions remain authoritative. Migration implementation and operational gaps are documented in [MIGRATIONS.md](MIGRATIONS.md).
