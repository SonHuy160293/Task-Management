# Codex Implementation Plan — Identity EF Core Domain Models and PostgreSQL Mappings

Historical model-only task scope. The subsequent, explicitly requested migration
runner work is documented in [MIGRATIONS.md](MIGRATIONS.md); it adds an initial
migration without seeding. The restrictions below describe the original task.

## Goal

Implement the **18 Identity & Access entities**, their **EF Core `IEntityTypeConfiguration<T>` mappings**, and their registration in the existing **Identity module `DbContext`**, based on the attached Identity ERD and PostgreSQL 18 SQL blueprint. These mappings will be used for **future** EF Core migrations; **do not create a migration now**.

Authoritative design inputs (attach these files to Codex or put them in the repo, for example under `docs/identity/`):

1. `identity_erd_postgresql18.md` — data dictionary, schema ownership, relationships, sizes and semantic rules.
2. `identity_postgresql18.sql` — authoritative relational constraints, indexes, defaults and delete behavior.
3. Existing repository `AGENTS.md`, `.editorconfig`, shared entity base class, and Identity module patterns.

If the Markdown and SQL disagree on physical constraints, use SQL as the physical baseline and report the discrepancy. Do not execute the SQL. This request intentionally changes **table and column identifier casing** from the original SQL blueprint.

## Hard requirements

1. **Implementation scope only:** Domain classes + EF Core Fluent configurations + registration in the existing Identity `DbContext`.
2. **Use the installed `create-ef-entity` skill**, repeating its workflow for each table: inspect repository conventions -> Domain model -> `IEntityTypeConfiguration<T>` -> DbContext. If the skill's sample paths assume `BookingSystem.<Module>`, adapt to the **current repository's actual Identity module name and directories**, rather than introducing `BookingSystem` code into another application.
3. Read root `AGENTS.md`, `src/Modules/AGENTS.md` and Identity module `AGENTS.md` if they exist; obey `.editorconfig` and existing patterns.
4. **NO migrations:** do not run `dotnet ef migrations add`, `dotnet ef database update`, create migration files, generate SQL scripts, or connect to a database.
5. **NO verification loop:** do not run builds, tests, migrations, Docker, scripts, or repeated automated validation unless a higher-priority repository instruction strictly requires one. Only do an in-editor/static consistency review.
6. No controllers, APIs, handlers, validators, repositories, DTOs, seeders, services, new features, or refactors outside the requested model/configuration/DbContext scope.
7. Keep Domain models persistence-ignorant: **no** EF attributes, no DbContext, no Npgsql dependencies in Domain.
8. Preserve **all 18 tables, all original columns, exact data types, nullability, lengths, keys, constraints, indexes, relationships and schema ownership** as far as EF Core model metadata can express them. Record migration-only items separately; do not silently weaken them.
9. All PKs are `Guid` generated as **UUID version 7**. Use `.NET Guid.CreateVersion7()` when creating new domain entities and configure PostgreSQL 18 `uuidv7()` as a database fallback/default where appropriate; preserve UUIDv7 CHECK constraints. Do not use `Guid.NewGuid()` or generate random v4 IDs.
10. **PascalCase** for all C# entity, property, navigation, `DbSet`, **database table**, and **database column** names. Use **singular** class names and **plural** table names. Preserve exact physical schema names `shared` and `dbo` (lowercase). Examples: `shared.Users`, `shared.RefreshTokens`, `dbo.MemberPermissionGrants`, columns `Id`, `PrimaryEmail`, `TenantId`, `CreatedAt`.
11. PostgreSQL PascalCase identifiers require quoted SQL identifiers; configure all table/column names explicitly and confirm no global snake_case naming convention changes them. Do not rename existing unrelated tables.
12. PascalCase applies to identifiers, **not stored string values**: maintain lowercase status/role/permission/scope codes from the ERD. If using C# enums, use explicit string converters that persist those values exactly.
13. No tenant-specific schemas. Every tenant-owned entity maps to the **one** `dbo` schema and carries `TenantId` (when specified in source). Shared/global catalog/auth entities map to `shared`.
14. Do not add invented fields such as `IsDeleted`, `RowVersion`, tenant-specific Role or normalized email columns. Do not invent cross-module FKs for `AccessScope.ResourceId`.

## Audit base class inheritance decision

Existing base:

```csharp
public abstract class Entity<TId> : IEntity<TId>, IAuditableEntity
{
    protected Entity() => CreatedAt = DateTimeOffset.UtcNow;
    protected Entity(TId id) { Id = id; CreatedAt = DateTimeOffset.UtcNow; }

    public TId Id { get; protected set; } = default!;
    public DateTimeOffset CreatedAt { get; protected set; }
    public string? CreatedBy { get; protected set; }
    public DateTimeOffset? UpdatedAt { get; protected set; }
    public string? UpdatedBy { get; protected set; }

    public void SetCreatedAudit(DateTimeOffset createdAt, string? createdBy) { CreatedAt = createdAt; CreatedBy = createdBy; }
    public void SetUpdatedAudit(DateTimeOffset updatedAt, string? updatedBy) { UpdatedAt = updatedAt; UpdatedBy = updatedBy; }
}
```

**Inherit `Entity<Guid>` only for these four lifecycle entities** (source SQL has `CreatedAt` and `UpdatedAt`):

- `User` (`shared.Users`)
- `Organization` (`shared.Organizations`)
- `PasswordCredential` (`shared.PasswordCredentials`)
- `OrganizationMember` (`dbo.OrganizationMembers`)

**Do not inherit `Entity<Guid>` for the other 14 entities.** They have dedicated event/lifecycle timestamps (e.g. `IssuedAt`, `AssignedAt`, `GrantedAt`, `OccurredAt`), are append-only records, join/grant records, short-lived secrets, or catalog entries. If they have a `CreatedAt` column in the ERD, declare it as an ordinary property without taking on the audit interface.

**Important audit/schema parity rules:**

- The source schema has **no `CreatedBy` or `UpdatedBy` columns anywhere**. For the four inheritors, explicitly `Ignore(x => x.CreatedBy)` and `Ignore(x => x.UpdatedBy)` in Fluent configuration; do **not** let future migrations silently create these columns. Do not confuse the real `Organization.CreatedByUserId : Guid` FK with inherited `CreatedBy : string?`.
- The source's four `UpdatedAt` columns are `timestamptz NOT NULL DEFAULT now()`, while the base property is `DateTimeOffset?`. **Keep the base class unchanged**, map the four `UpdatedAt` columns as DB-required, configure the SQL default, and ensure entity initialization/save conventions will not explicitly persist NULL. Flag this nullability mismatch in the final report if it cannot be resolved with the existing pattern.
- If the repository has an audit interceptor, do not duplicate its behavior. Because `CreatedBy`/`UpdatedBy` are ignored, actor audit values will **not be persisted** unless a later, separately approved schema change adds those columns.
- Give standalone entities their own `Guid Id` and only the source-defined timestamps. Implement `IEntity<Guid>` only if repository contracts require it; do not implement `IAuditableEntity` for standalone entities.

## Entity and table inventory — exactly 18

| Phase | Schema | C# class | PostgreSQL table | Inherit `Entity<Guid>`? |
|---|---|---|---|---|
| A | `shared` | `User` | `Users` | **Yes** |
| A | `shared` | `Organization` | `Organizations` | **Yes** |
| A | `shared` | `PasswordCredential` | `PasswordCredentials` | **Yes** |
| A | `shared` | `AuthSession` | `AuthSessions` | No |
| A | `shared` | `RefreshToken` | `RefreshTokens` | No |
| A | `shared` | `OneTimeToken` | `OneTimeTokens` | No |
| B | `shared` | `Permission` | `Permissions` | No |
| B | `shared` | `Role` | `Roles` | No |
| B | `shared` | `RolePermission` | `RolePermissions` | No |
| B | `shared` | `PlatformRoleAssignment` | `PlatformRoleAssignments` | No |
| C | `dbo` | `OrganizationMember` | `OrganizationMembers` | **Yes** |
| C | `dbo` | `AccessScope` | `AccessScopes` | No |
| C | `dbo` | `Invitation` | `Invitations` | No |
| C | `dbo` | `InvitationRoleGrant` | `InvitationRoleGrants` | No |
| C | `dbo` | `MemberRoleAssignment` | `MemberRoleAssignments` | No |
| C | `dbo` | `MemberPermissionGrant` | `MemberPermissionGrants` | No |
| D | `dbo` | `SecurityAuditEvent` | `SecurityAuditEvents` | No |
| D | `shared` | `PlatformSecurityAuditEvent` | `PlatformSecurityAuditEvents` | No |

All tables use an `Id` UUIDv7 PK. Do not create business entities or cross-module navigation entities for teams/projects/tasks/reviews.

## Step-by-step execution plan

### Phase 0 — Discover repository conventions and reconcile schema

1. Read applicable `AGENTS.md` files, `.editorconfig`, `create-ef-entity` skill, shared `Entity<TId>`, existing module entity/configuration examples, Identity `DbContext`, and any audit interceptor or custom EF conventions.
2. Identify actual project/namespace/path: `<Solution>.Identity.Domain/Models`, `<Solution>.Identity.Infrastructure/Persistence/Configurations`, and existing Identity `DbContext`; **do not invent paths**.
3. Inspect for existing entities/configurations first. **Update** them if present instead of creating duplicate models.
4. Treat the supplied SQL/ERD as the full database contract and prepare a 1:1 mapping inventory. Use original `snake_case` SQL as source for types/constraints, but convert resulting table/column identifiers to PascalCase.
5. Inspect Npgsql/EF Core versions to choose supported Fluent API patterns, without installing/upgrading packages.

### Phase A — Global users, organizations, credentials and sessions

Create Domain models, configurations and DbSets, one table at a time, in this order:

1. `User` (`Entity<Guid>`), unique case-insensitive `PrimaryEmail`, `DisplayName` length 160, `Locale`, `TimeZone`, `Status`, nullable verification/login timestamps.
2. `Organization` (`Entity<Guid>`), unique case-insensitive `Slug`, `CreatedByUserId` FK to `User`, `Status`, `SuspendedAt`.
3. `PasswordCredential` (`Entity<Guid>`), unique `UserId` FK, Argon2id `PasswordHash` max 512, `FailedAttempts`, `LockedUntil`, `PasswordChangedAt`.
4. `AuthSession` (standalone), `UserId` FK, nullable `ClientIp` (`inet`), `UserAgent`, `CreatedAt`, `LastSeenAt`, `ExpiresAt`, `RevokedAt`.
5. `RefreshToken` (standalone), `SessionId` FK, unique 32-byte `TokenHash` (`bytea`), nullable self-reference `ReplacedByTokenId`, `IssuedAt`, `ExpiresAt`, `ConsumedAt`, `RevokedAt`.
6. `OneTimeToken` (standalone), `UserId` FK, `Purpose`, unique 32-byte `TokenHash`, `CreatedAt`, `ExpiresAt`, `ConsumedAt`.

### Phase B — Global RBAC catalog and platform administration

7. `Permission`: unique `Code`, `Module`, `ScopeKind`, `Description`, flags, `CreatedAt`, alternate unique key `(Id, ScopeKind)`.
8. `Role`: unique `Code`, `DisplayName`, `ScopeKind`, description, flags, `CreatedAt`, alternate unique key `(Id, ScopeKind)`.
9. `RolePermission`: composite FKs `(RoleId, ScopeKind)` -> `Role`, `(PermissionId, ScopeKind)` -> `Permission`, unique `(RoleId, PermissionId)`.
10. `PlatformRoleAssignment`: `UserId` FK, composite `(RoleId, ScopeKind)` -> `Role` with `ScopeKind='platform'`, nullable `AssignedByUserId`, `AssignedAt`, `RevokedAt`, active-only unique index on `(UserId, RoleId)`.

### Phase C — Tenant membership, scoped roles and extra permissions

11. `OrganizationMember` (`Entity<Guid>`): `TenantId` -> `Organization`, `UserId` -> `User`, membership type/status, `JoinedAt`, `RemovedAt`; unique `(TenantId, UserId)` and alternate `(TenantId, Id)`.
12. `AccessScope`: `TenantId`, `ScopeKind`, `ResourceId` **without cross-module FK**, `Status`, `CreatedAt`, `ArchivedAt`; unique `(TenantId, ScopeKind, ResourceId)` and alternate `(TenantId, Id, ScopeKind)`.
13. `Invitation`: `TenantId`, email and token hash, status, invited-by **composite tenant-scoped member FK**, accepted-by user FK, lifecycle timestamps; unique pending email **per tenant, case-insensitively**.
14. `InvitationRoleGrant`: same-tenant composite FK to `Invitation`, same-tenant/scope-kind composite FK to `AccessScope`, role/scope-kind composite FK to `Role`.
15. `MemberRoleAssignment`: same-tenant member/scope composite FKs, role/scope-kind composite FK, assignment actor, reason, expiry and revocation; active-only unique index.
16. `MemberPermissionGrant`: same-tenant member/scope composite FKs, permission/scope-kind composite FK, `Justification`, expiry and revocation; active-only unique index. This is **direct additional ALLOW access only**, not DENY/override access.

### Phase D — Append-only audit records

17. `SecurityAuditEvent` (`dbo`): `TenantId`, actor IDs as historical snapshots **without actor FK**, event fields, nullable `ClientIp` (`inet`) and `CorrelationId`, bounded `Details` (`jsonb`), `OccurredAt`. Only `TenantId` is a tenant FK.
18. `PlatformSecurityAuditEvent` (`shared`): optional `TargetTenantId` FK, historical actor ID without actor FK, equivalent event fields; map as append-only standalone entity.

### Phase E — Static mapping review and completion report

Review file content only (no builds/tests or database activity) and report:

- 18 Domain models, 18 `IEntityTypeConfiguration<T>` mappings, and 18 DbSets (unless a project convention already registers entities without DbSets).
- Four `Entity<Guid>` inheritors and fourteen standalone entities.
- Exact PascalCase schema/table/column mappings, all UUIDv7 PKs, nullability, types, length/precision, defaults, indexes, CHECK constraints and FK/delete behaviors.
- Composite tenant/scope FKs and alternate unique keys.
- Every intentional exception or relational feature that EF Core Fluent API cannot represent before a future migration.
- Explicit confirmation: **no migration created, no verification loop executed**.

## Mapping checklist (applies to all 18)

- Configure `ToTable("PluralPascalCase", "shared" / "dbo")`; explicitly `HasColumnName("PascalCase")` on **every** mapped property, including inherited `Id`, `CreatedAt`, and `UpdatedAt`.
- Configure `Guid` PK and `HasDefaultValueSql("uuidv7()")` if compatible with application-generated UUIDv7; ensure preassigned UUIDv7 is not replaced. Configure UUID version 7 CHECK if supported by provider.
- PostgreSQL `timestamptz` maps to `DateTimeOffset`/`DateTimeOffset?` and UTC offsets only. `varchar(n)` -> string `HasMaxLength(n)`; `text` -> string; `integer` -> int; `boolean` -> bool; `bytea` -> `byte[]`; `inet` -> `System.Net.IPAddress?`; `jsonb` -> an Npgsql-supported JSON type (e.g. `JsonDocument`/`JsonElement`) according to existing project patterns.
- Explicitly preserve `varchar` lengths, nullable fields, default values, status/purpose/scope CHECKs, lower/upper bounds, time-order CHECKs, 32-byte token hash constraints, and JSON-object check.
- Use `HasAlternateKey` / `HasPrincipalKey` for composite referenced keys, and `.HasForeignKey(...)` for tenant- and scope-safe relationships. Use restrictive deletes (`Restrict`/`NoAction`) as in DDL; `RefreshToken.ReplacedByTokenId` is an intentional nullable self-FK with `SET NULL` behavior.
- Preserve partial unique indexes such as `RevokedAt IS NULL`, invitation pending email, and query indexes for sessions/tokens/audits. **Do not replace unique `lower(email)` / `lower(slug)` expression indexes with ordinary case-sensitive indexes.** If expression indexes cannot be expressed in the installed EF Core/Npgsql Fluent mapping, report their exact required SQL for a future migration; do not invent extra columns or create a migration now.
- Database identifiers and quoted expressions in CHECK/filter/default SQL must use **PascalCase property/column names** (e.g. `"RevokedAt"`, not `revoked_at`). **Do not change the lowercased values** like `'active'`, `'guest'`, `'organization'`.
- `dbo` row-level security policies (RLS) and app tenant session configuration belong in a **future migration/infrastructure task**; record that requirement, do not implement policies in this task.
- Audit events are **append-only** by behavior; don't add EF soft-delete or cascade deletion. Never store plain refresh tokens, reset tokens, or password values.
- Domain may have minimal navigation properties for same-module relationships, but avoid giant bidirectional graphs and any navigation/physical FK to other bounded contexts.

## Acceptance criteria

- [ ] All 18 source tables represented exactly once by correctly named C# Domain models.
- [ ] Four specified models inherit the existing `Entity<Guid>` without modifying the base; remaining 14 do not.
- [ ] No extra columns accidentally introduced by audit inheritance.
- [ ] All tables/columns configured as PascalCase while schemas remain `shared` and `dbo`.
- [ ] All IDs use `Guid` UUIDv7; no `Guid.NewGuid()`.
- [ ] Column type, nullability, lengths, defaults, constraints, indexes and FK graph match source SQL semantically.
- [ ] Cross-tenant relationships protected by composite FK mappings, including scope-kind matching.
- [ ] Direct permission grants are modeled separately from role permissions.
- [ ] No cross-module DB FK invented for `AccessScope.ResourceId`.
- [ ] Existing Identity DbContext updated; no duplicate DbContext.
- [ ] No migration, database changes, new features, test/build loop or verification scripts.
- [ ] Final response lists files changed and unresolved EF-vs-DDL parity gaps, if any.

## Explicit out of scope

No migrations, database creation, initial seed data, application services, APIs, controllers, CQRS commands, validation handlers, auth/token business logic, tenant resolution middleware, RLS implementation, performance reviews, team/project/task models, or verification loop.
