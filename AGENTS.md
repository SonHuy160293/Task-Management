# AGENTS.md — DevFlow

Instructions for coding agents working in this repository.

Read this file before changing the solution. A deeper `AGENTS.md` adds or narrows
instructions for its subtree; this root file continues to apply unless the deeper
file explicitly overrides it.

## Repository map

DevFlow is a .NET 10 modular-monolith starter built with ASP.NET Core and Clean
Architecture. Business capabilities belong under `src/Modules/DevFlow.<Module>`.

| Area | Path | Guidance |
| --- | --- | --- |
| Shared technical primitives | `src/BuildingBlocks/` | `src/BuildingBlocks/AGENTS.md` |
| Cross-module contracts | `src/Contracts/` | Add contracts only for real module boundaries |
| Bounded contexts | `src/Modules/` | `src/Modules/AGENTS.md` |
| Identity and access | `src/Modules/DevFlow.Identity/` | `src/Modules/DevFlow.Identity/AGENTS.md` |
| Task management | `src/Modules/DevFlow.Tasks/` | `src/Modules/DevFlow.Tasks/AGENTS.md` |
| Deployment and telemetry | `deploy/` | `deploy/README.md` and `deploy/observability/README.md` |
| Architecture | repository-wide | `ARCHITECTURE.md` |
| Repository skills | `.codex/skills/` | Use a matching skill when the task fits |

Inspect `DevFlow.slnx`, project files, and active host registrations before assuming
that a runtime component or test project exists. Empty directories and design
documents do not establish implemented behavior.

## Guidance ownership

- This file owns repository-wide architecture invariants, coding conventions, and
  the work protocol.
- `src/Modules/AGENTS.md` owns module boundaries and standard layer responsibilities.
- Each module's `AGENTS.md` owns its layout and module-specific integration rules.
- Layer-level `AGENTS.md` files own specialized rules, such as Identity's domain
  model construction, identifier types, and auditing conventions.

Keep changing capability summaries, implementation progress, and operational gaps
in the owning module's documentation under `docs/`, rather than repeating them in
agent instructions. Verify those summaries against active source before relying
on them; a planned endpoint or commented-out implementation is not working behavior.

## Architecture invariants

Keep dependencies pointing inward:

```text
Module.API            -> Module.Application + Module.Infrastructure + Contracts + BuildingBlocks
Module.Worker         -> Module.Application + Module.Infrastructure + Contracts + BuildingBlocks
Module.MigrationRunner -> Module.Infrastructure + BuildingBlocks
Module.Infrastructure -> Module.Application + Module.Domain + Contracts + BuildingBlocks
Module.Application    -> Module.Domain + Contracts + BuildingBlocks
Module.Domain         -> BuildingBlocks only
Contracts             -> BuildingBlocks only when needed
BuildingBlocks        -> other BuildingBlocks, one-way and without cycles
```

- Application must never reference Infrastructure.
- Modules must not reference another module's Domain or Infrastructure. Integrate
  through contracts or an Application-owned abstraction.
- Controllers translate HTTP input and send commands or queries through
  `DevFlow.SharedKernel.Cqrs.ISender`. Do not inject a `DbContext`, repository,
  identity manager, or domain service into a controller.
- DevFlow uses its own CQRS abstractions. Do not add MediatR, `MediatR.IRequest`, or
  MediatR handlers.
- Preserve the registered pipeline order: validation, logging, performance,
  tracing, transaction, then handler.
- The transaction behavior owns command transactions and persistence completion.
  Handlers and repositories must not call `SaveChangesAsync` when that behavior
  owns the commit. Queries do not open command transactions.
- Let `DomainException`-based failures reach `GlobalExceptionHandlingMiddleware`;
  avoid broad controller `try/catch` blocks around ordinary application flow.
- For HTTP request-model placement and centralized failure propagation, follow
  [Modules API conventions](src/Modules/AGENTS.md#api-request-contracts-and-failure-handling).

Run `powershell -File scripts/Test-Architecture.ps1` after changing project
references or architectural boundaries.

## Coding and persistence conventions

- The SDK is pinned by `global.json`; target framework and analyzer settings come
  from `Directory.Build.props`.
- Nullable reference types and implicit usings are enabled; C# language version is
  `latest`. Build analyzers are authoritative for style.
- NuGet versions are centralized in `Directory.Packages.props`. Keep versions out
  of individual `PackageReference` elements.
- Follow the existing file-scoped namespace, PascalCase type/member, `_camelCase`
  private-field, and async-method conventions. Pass `CancellationToken` through
  asynchronous boundaries.
- Prefer small, sealed request/handler/validator types where extension is not
  intended. Keep transport DTOs separate from EF entities.
- For command and query folder, file, and namespace conventions, follow
  [Modules CQRS file layout](src/Modules/AGENTS.md#cqrs-file-layout).
- Domain projects contain business state and invariants, not EF attributes, HTTP
  concerns, configuration, or infrastructure services.
- EF mappings live in Infrastructure. Use plural PascalCase table names and
  PascalCase columns; quote those identifiers in custom PostgreSQL SQL.
- Each module owns its persistence. Cross-module IDs are scalar references, not
  cross-module database navigations or foreign keys.
- Use `DateTimeOffset` in UTC for persisted instants. Do not introduce local-time
  persistence.
- Never log credentials, access or refresh tokens, secrets, raw sensitive payloads,
  or other authentication material.

## Work protocol

1. Read the nearest applicable `AGENTS.md`, `ARCHITECTURE.md`, and exact source
   files involved.
2. Inspect the closest implementation and its public signatures before adding a
   pattern. Repository source is more authoritative than examples or memory.
3. Keep the change within the requested module and layer. Do not silently add
   optional runtime projects or infrastructure.
4. Use the smallest relevant feedback loop: targeted build/test first; architecture
   checks for dependency changes; documentation review for documentation-only work.
5. Never claim a check passed unless it ran successfully. Report environment or
   pre-existing failures precisely.

For an Application command/query (including API, worker, or consumer use cases)
or EF model, use the corresponding repository skill under `.codex/skills/` in
addition to these invariants.

<!-- CODEGRAPH_START -->
## CodeGraph

In repositories indexed by CodeGraph (a `.codegraph/` directory exists at the repo root), reach for it BEFORE grep/find or reading files when you need to understand or locate code:

- **MCP tool** (when available): `codegraph_explore` answers most code questions in one call — the relevant symbols' verbatim source plus the call paths between them, including dynamic-dispatch hops grep can't follow. Name a file or symbol in the query to read its current line-numbered source. If it's listed but deferred, load it by name via tool search.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.

If there is no `.codegraph/` directory, skip CodeGraph entirely — indexing is the user's decision.
<!-- CODEGRAPH_END -->
