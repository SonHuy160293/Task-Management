# Identity module rules

Root `AGENTS.md` and `src/Modules/AGENTS.md` apply here.

Identity owns global users and credentials, organizations and membership, RBAC and
scopes, invitations, sessions/tokens, and security audit records. It does not own
task-management business data.

## Module layout

Paths below are relative to `src/Modules/DevFlow.Identity/`:

```text
DevFlow.Identity.API/
  Controllers/
  Health/
  Security/
  Program.cs
DevFlow.Identity.Application/
  Abstractions/
    Persistence/
    Repositories/
  Features/
    Auth/Commands/Registration/
    Users/Queries/GetUser/
  DependencyInjection/
DevFlow.Identity.Domain/
  Models/
  AGENTS.md
DevFlow.Identity.Infrastructure/
  Persistence/
    Core/
    DbContexts/IdentityDbContext.cs
    Configurations/
    Repositories/
  DepedencyInjection/
DevFlow.Identity.MigrationRunner/
  Program.cs
  appsettings.json
  AGENTS.md
```

`DepedencyInjection` above preserves the existing Infrastructure folder spelling.
Directory and file presence describe placement, not feature readiness.

- Follow [Modules CQRS file layout](../AGENTS.md#cqrs-file-layout) for commands
  and queries; Registration and GetUser are the reference implementations.
- Follow [Modules API conventions](../AGENTS.md#api-request-contracts-and-failure-handling)
  for transport models and centralized failures. Registration's HTTP request
  belongs in `DevFlow.Identity.API/Contracts/Auth/RegisterRequest.cs`, with
  namespace `DevFlow.Identity.API.Contracts.Auth`.
- Put Identity repository interfaces in `Application/Abstractions/Repositories`
  and module-specific persistence contracts in `Application/Abstractions/Persistence`.
  Inspect shared persistence contracts before adding another abstraction.
- Put EF repository implementations in `Infrastructure/Persistence/Repositories`,
  persistence support such as `RepositoryBase`, `IdentityUnitOfWork`, and
  `SqlConnectionFactory` in `Persistence/Core`, and EF mappings in
  `Persistence/Configurations`. `IdentityDbContext` belongs in
  `Persistence/DbContexts`.
- Keep model construction, identifier, audit, and schema-parity rules in
  [Domain AGENTS.md](DevFlow.Identity.Domain/AGENTS.md). Read it before changing
  models or their mappings.

## Composition and implementation evidence

Trace `DevFlow.Identity.API/Program.cs` to the Application and Infrastructure
extension methods it actually invokes before changing DI, connection settings,
middleware, or pipeline registration. Inspect existing extension entry points
before adding another one; the presence of a registration method does not mean
the host calls it.

Verify controllers, active handlers/validators, service registrations, and
middleware together before describing an endpoint as implemented. Keep progress
and operational details in `docs/identity/`; its task cards describe planned
contracts until verified against source and acceptance checks.

## Identity boundaries

- Preserve the separation between a global `User`, an `Organization`, and an
  `OrganizationMember`. Tenant membership is not the user record.
- Preserve platform roles separately from organization/scoped grants.
- Store only hashes for passwords and one-time/refresh tokens. Never expose or log
  stored hashes or plaintext authentication material.
- Treat security audit entities as append-only. Do not turn audit history into
  ordinary mutable aggregates.
- Tenant-owned `dbo` data and global/shared data use the schemas established by the
  mappings. Do not copy SQL Server conventions into this PostgreSQL module.
- Explicit composite relationships and restricted deletes protect tenant isolation
  and audit history. Do not simplify them without checking the ERD and SQL design.
- Verify row-level security, trusted `SET LOCAL app.tenant_id`, runtime connection
  roles, append-only grants, migrations, and operational retention against actual
  runtime and deployment configuration. Do not claim they are active because the
  design documents describe them.

Use PostgreSQL-compatible types and SQL (`uuid`, `timestamptz`, `inet`, `jsonb`),
and quote PascalCase identifiers in raw SQL. Controllers must use `ISender`;
Application owns abstractions and Infrastructure implements them.

## Schema references

The schema and model reference is `docs/identity/identity_erd_postgresql18.md`.
Reference SQL is a design artifact, not migration history. Reconcile it with the
current model, EF mappings, and any owning-module migrations before changing schema
behavior. Do not rewrite deployed migration history.

