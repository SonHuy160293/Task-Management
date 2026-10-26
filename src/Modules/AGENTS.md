# Module rules

Root `AGENTS.md` applies here. Each bounded context under this directory owns its
business rules and technical implementation.

## Module boundaries

| Module | Folder | Ownership |
| --- | --- | --- |
| Identity | `DevFlow.Identity/` | Global accounts and credentials, organizations and membership, access control, invitations, sessions/tokens, and security audit records |
| Notification | `DevFlow.Notification/` | Durable email intake/delivery, templates, provider outcomes and suppression; never account verification |
| Tasks | `DevFlow.Tasks/` | Task-management business rules and data; Identity IDs are external scalar references |

Do not infer additional modules from package references, deployment options, or
documentation examples. Read each module's `AGENTS.md` for its layout and specific
rules; inspect active source for implementation status.

## Standard layout

```text
src/Modules/DevFlow.<Name>/
├── DevFlow.<Name>.API
├── DevFlow.<Name>.Application
├── DevFlow.<Name>.Domain
├── DevFlow.<Name>.Infrastructure
├── DevFlow.<Name>.Worker           # optional
└── DevFlow.<Name>.MigrationRunner  # optional
```

Add optional projects only when a real requirement exists. Register every project
in `DevFlow.slnx`, keep package versions central, add module-level `AGENTS.md`, and
extend architecture checks when a new project kind introduces a new boundary.

## Layer ownership

- API owns transport, middleware, host configuration, and dependency composition.
- Application owns use cases, commands, queries, handlers, validators, DTOs, and
  repository/integration abstractions.
- Domain owns entities, value objects, invariants, and domain events.
- Infrastructure owns EF contexts/configurations, repository implementations, and
  external service adapters.
- Worker is an optional executable composition root for independently deployed
  background work. It references its own Application and Infrastructure, Contracts,
  and BuildingBlocks, never another module's internals or API. APIs do not reference
  executable Workers.
- MigrationRunner, when present, applies the owning module's migrations. Do not
  migrate a database implicitly during API startup.

Reuse the persistence contracts in
`DevFlow.SharedKernel.Abstractions.Persistence`; keep domain-specific interfaces in
Application and implementations in Infrastructure. Organize new use cases by
feature rather than by a repository-wide collection of commands or handlers.

## CQRS file layout

Place each Application use case in its own folder:

- Commands: `Features/<Area>/Commands/<UseCase>/`.
- Queries: `Features/<Area>/Queries/<UseCase>/`.

Keep the request, handler, validator (when input validation is needed), and local
response DTOs together in that folder, with one top-level type per `.cs` file
named after the type. Match the namespace to the folder. This applies to API,
worker, and consumer use cases alike; each use case gets its own folder.

Use these Identity implementations as references:

- `DevFlow.Identity.Application/Features/Auth/Commands/Registration/`:
  `RegisterAccountCommand.cs`, `RegisterAccountHandler.cs`,
  `RegisterAccountValidator.cs`, and `RegistrationAcceptedResponse.cs`.
- `DevFlow.Identity.Application/Features/Users/Queries/GetUser/`:
  `GetUserQuery.cs`, `GetUserQueryHandler.cs`, and `GetUserQueryValidator.cs`.

When moving existing types, update callers' namespaces and preserve assembly
discovery, public signatures, validation, and handler behavior. Verify every
changed command and query follows this layout before completing the change.

## API request contracts and failure handling

HTTP request models belong in the owning API project's
`Contracts/<Area>/<RequestName>.cs`, with one top-level type per file and a
matching namespace. Controller files contain controller types only. Map these
transport models to Application commands or queries; API-local contracts are
distinct from cross-module contracts under `src/Contracts/`.

Controllers translate input, send through `ISender`, and choose the successful
HTTP response. Centralized failure handling owns validation errors, error-code
mapping, status codes, and error envelopes. Let supported `DomainException` and
`CqrsValidationException` failures reach `GlobalExceptionHandlingMiddleware`.
Keep `IValidationResult` type checks, `ValidationProblemDetails` construction,
and application-error-to-HTTP switches out of controller actions.

Trace the validator, pipeline, handler, and middleware before changing a failure
path. Middleware catches thrown exceptions; it does not inspect returned failed
`Result` objects. Result-based paths need a centralized result-to-exception
bridge or an explicit change to exception
propagation before controller failure branches can be removed. Reading
`result.Value` on a failed result throws `InvalidOperationException`, which is
not a substitute for the intended validation or domain error.

Before completing an API change, check request-model placement and verify that
invalid input and handler failures reach centralized handling with the intended
status, error details, and trace ID. Preserve documented statuses such as 400,
404, 409, and 503; extend shared exception mapping when a required status is not
supported rather than recreating that mapping in a controller.

