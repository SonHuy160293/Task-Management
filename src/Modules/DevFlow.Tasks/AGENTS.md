# Tasks module rules

Root `AGENTS.md` and `src/Modules/AGENTS.md` apply here.

Tasks is the runnable starter module for task-management capabilities. Keep task
business rules and implementations inside this bounded context.

## Current state

| Project | Responsibility and status |
| --- | --- |
| `DevFlow.Tasks.API` | Executable ASP.NET Core host with controllers, global exception/correlation middleware, and live/ready health routes |
| `DevFlow.Tasks.Application` | Custom CQRS registration and pipeline; no use cases yet |
| `DevFlow.Tasks.Domain` | Domain layer skeleton; no task models yet |
| `DevFlow.Tasks.Infrastructure` | Registration skeleton; no database provider, context, repository, or unit of work yet |

Do not assume authentication, authorization policies, persistence, migrations,
business endpoints, workers, or external integrations exist.

## Adding behavior

- Put task invariants and state transitions in Domain; put commands, queries,
  validators, handlers, DTOs, and interfaces in Application.
- Controllers send requests through `ISender`. Choose a shared controller base only
  after verifying its authentication and response behavior are registered by the
  host.
- Before the API sends any request, register a real `IUnitOfWork` in Infrastructure:
  the transaction pipeline resolves it even for queries. Do not add a dummy unit of
  work to conceal missing persistence.
- If persistence is required, select the provider and schema deliberately from the
  feature requirements. Optional PostgreSQL and SQL Server deployment services do
  not choose a provider for this module.
- Handlers and repositories do not call `SaveChangesAsync` when the transaction
  pipeline owns the command commit. Query handlers do not start command
  transactions.
- Treat Identity IDs as external scalar identifiers. Do not reference Identity
  Domain/Infrastructure or create cross-module EF navigations.
- Preserve middleware ordering intentionally when adding authentication,
  authorization, Swagger, CORS, or additional middleware.

Use `.codex/skills/add-cqrs-api-feature` for end-to-end API use cases and
`.codex/skills/create-ef-entity` for domain/EF persistence changes.

