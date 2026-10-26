# Identity database migrations

Supported deployment: use [module onboarding](../../deploy/README.md). Each
`scripts/devflow.* up` runs a fresh migration job and reapplies runtime grants
before replacing the selected module's runtime containers. The worker and HTTP
host use the same owning database. Follow the new deployment guide for normal use;
the historical learning-stack verification notes below are not current commands.

Identity migrations run through `DevFlow.Identity.MigrationRunner`, an executable
registered in `DevFlow.slnx`. It references Identity Infrastructure and shared
Observability, registers `AddPersistence` only, calls `MigrateAsync` with the host
shutdown token, and exits. A failed run returns exit code 1. The API never migrates
its database. There is no seeding: no accounts, roles, permissions, memberships,
or sample data are inserted.

## Reference analysis and adaptation

The source pattern is
`D:\Projects\BookingSystem\BookingSystem\src\Modules\BookingSystem.Cinema\BookingSystem.Cinema.MigrationRunner`.
Cinema uses a generic host, environment-specific JSON, user secrets, environment
variables, Serilog/OTLP, a scoped DbContext, and a one-shot EF migration call.
Its Docker migration target waits for PostgreSQL health; its API waits for the
runner's successful completion.

DevFlow preserves that lifecycle and Compose ordering. It uses Identity's existing
`ConnectionStrings:ApplicationDb`, DbContext and persistence registration. It does
not register Identity security/CQRS or require Redis, JWT, or HMAC keys to
migrate. Logs go to the container console; telemetry is optional. Failures log the
exception type without raw provider/configuration messages that can reveal secrets.
Command/connection error logs are suppressed by default. Runtime EF command retries
remain disabled because registration uses explicit application transactions.

## Schema ownership

PostgreSQL **18 or later** is required by the existing `uuidv7()` defaults and
`uuid_extract_version()` checks. Do not use the optional PostgreSQL 17 development
service for this schema.

`DevFlow.Identity.Infrastructure/Persistence/Migrations` owns the initial migration,
its designer, and `IdentityDbContextModelSnapshot`. `InitialIdentity` creates the
18 mapped tables in `shared` and `dbo`, with PascalCase physical identifiers,
mapped checks, defaults, composite keys/foreign keys, restricted deletes, and
partial indexes. It also creates these three SQL expression indexes, which are
deliberately absent from the EF model snapshot:

| Index | Expression |
| --- | --- |
| `shared."UxUsersPrimaryEmailCi"` | Unique `lower("PrimaryEmail")` on `shared."Users"` |
| `shared."UxOrganizationsSlugCi"` | Unique `lower("Slug")` on `shared."Organizations"` |
| `dbo."UxInvitationsPendingEmail"` | Unique `("TenantId", lower("InviteeEmail"))` where `"Status" = 'pending'` |

Dropping their owning tables in `Down` also drops these indexes. Preserve them
explicitly in future migrations; EF cannot detect their removal or drift.
`__EFMigrationsHistory` uses EF's default location in the database's default schema.
Rerunning the runner applies only pending migrations.

Tenant RLS policies, trusted `SET LOCAL app.tenant_id`, restricted runtime roles,
append-only audit grants, and retention remain separate work. The local Docker
stack uses the PostgreSQL owner for convenience and does not demonstrate those
production controls. This migration implements the mapped schema, not the entire
reference SQL blueprint.

## Run locally

Supply a connection to an isolated PostgreSQL 18 database through the process
environment or this runner's user secrets:

```powershell
dotnet user-secrets set 'ConnectionStrings:ApplicationDb' 'Host=localhost;Port=5432;Database=DevFlowIdentity;Username=devflow_identity;Password=YOUR_LOCAL_PASSWORD' --project src/Modules/DevFlow.Identity/DevFlow.Identity.MigrationRunner
dotnet run --project src/Modules/DevFlow.Identity/DevFlow.Identity.MigrationRunner
```

Configuration precedence is `appsettings.json`, environment-specific JSON,
runner user secrets, environment variables, then command-line options. JSON files
are loaded relative to the executable, including published runs. Environment
selection uses `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT`, then Production.
No credentials are committed. Secrets for the API and runner are separate stores.

## Add or inspect migrations

Use a .NET 10 `dotnet-ef` tool compatible with the centrally pinned EF packages:

```powershell
dotnet ef migrations add DescribeIdentityChange --project src/Modules/DevFlow.Identity/DevFlow.Identity.Infrastructure --context IdentityDbContext --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/Modules/DevFlow.Identity/DevFlow.Identity.Infrastructure --context IdentityDbContext
dotnet ef migrations script --idempotent --project src/Modules/DevFlow.Identity/DevFlow.Identity.Infrastructure --context IdentityDbContext --output artifacts/identity-migrations.sql
powershell -File scripts/Test-Architecture.ps1
```

The Infrastructure design-time factory avoids API startup and does not require a
database for scaffolding/scripts/model comparison. It reads
`ConnectionStrings__ApplicationDb`; its credential-free fallback is for tooling
only. It does not read the runner's JSON or user secrets. Prefer the runner for
applying changes; provide an explicit connection for any EF database command.

Review generated SQL before applying future changes. The initial migration targets
a new database; do not run it over tables created manually from the design SQL.
Existing installations need a reviewed baseline/data migration. Production
deployments should run a single migration job with a DDL-capable credential before
rolling out API replicas, then use a separate restricted API credential.

## Docker and verification

See [Docker deployment](../../deploy/README.md#startup-implementation)
for the PostgreSQL -> runner -> API stack and configuration. Run it against a fresh
development volume to verify initial creation and absence of seed rows. After the
runner exits successfully, repeat it with `docker compose ... run --rm
identity-migrationrunner`; it should report the database is up to date.

For database-backed registration verification, point `DEVFLOW_TEST_POSTGRES` at a
migrated **isolated test database**, then run:

```powershell
dotnet test tests/DevFlow.Identity.Tests/DevFlow.Identity.Tests.csproj
```

Those tests insert temporary users, credentials, and challenges and clean up
committed rows; those are test fixtures, not migration seed data. Redis-specific
tests require `DEVFLOW_TEST_REDIS`. Do not use production databases for tests.

### Verification recorded on 2026-10-25

- Runner build and both Identity Docker targets succeeded; existing repository
  analyzer warnings remain.
- A fresh isolated PostgreSQL 18 Docker volume received exactly 18 business tables,
  all three expression indexes, and one migration-history row. Every business
  table had zero rows before and after the registration tests.
- A second containerized runner completed successfully without adding migrations
  or data. Missing configuration and unreachable PostgreSQL both returned exit
  code 1 without raw provider errors or credentials in the runner log.
- Compose waited for successful migration completion, then the Identity API became
  healthy. Both `/health/ready` and `/health/live` returned Healthy.
- All 29 Identity tests passed with PostgreSQL and Redis enabled, with zero skips.
  These include unique-email, concurrent atomic registration and rollback checks.
- EF reported no pending model changes and generated an idempotent SQL script.
  Architecture checks passed for 15 projects. Default Tasks, Identity, and both
  Identity telemetry Compose configurations validated.

Telemetry backend ingestion and production tenant/role isolation were not tested.
The temporary verification stack and volumes were removed. The normal local
`devflow-identity` stack was then started on API port 8081 and PostgreSQL port 5432,
with fresh empty tables and generated secrets in the ignored
`deploy/env/.identity.env`. The pre-existing standalone Redis was left running.
