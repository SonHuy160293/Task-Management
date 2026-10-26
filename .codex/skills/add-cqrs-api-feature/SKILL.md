---
name: add-cqrs-api-feature
description: Add, modify, or refactor DevFlow Application commands and queries, including API, worker, and consumer use cases, with custom CQRS, validation, handlers, and required persistence.
---

# Add or refactor a DevFlow CQRS feature

Read ARCHITECTURE.md, applicable AGENTS.md files if present, and the target module.
Current starter: src/Modules/DevFlow.Tasks/DevFlow.Tasks.{API,Application,Domain,Infrastructure}.
Shared primitives live under src/BuildingBlocks; cross-module contracts belong in
src/Contracts. Inspect actual source signatures rather than assuming examples exist.

Resolve route, input/output contract, query versus command, expected failures, and
access requirements from the request and nearby code. Keep changes within the use
case. Tasks currently has no business endpoints, database, authentication policy,
repositories, or IUnitOfWork implementation; do not assume those are configured.

## Use-case layout

Follow [Modules CQRS file layout](../../../src/Modules/AGENTS.md#cqrs-file-layout).
Before creating or reorganizing types, inspect the reference that matches the
request: Identity's `Features/Auth/Commands/Registration/` for commands or
`Features/Users/Queries/GetUser/` for queries. Inspect the request, handler,
validator, and any local response DTOs as separate files, then apply that layout
in the target module. For a refactor, update callers' imports and preserve
behavior and assembly-based registration.

## Flow and registration

For HTTP endpoints, follow [Modules API conventions](../../../src/Modules/AGENTS.md#api-request-contracts-and-failure-handling).
Resolve the API-local request-model path before editing the controller; for
Identity registration it is `DevFlow.Identity.API/Contracts/Auth/RegisterRequest.cs`.
Inspect the active Application DI registration, its registered validation
behavior, handler failures, and `GlobalExceptionHandlingMiddleware` together.
Establish how every failure reaches
centralized handling before writing a success-only action; returned failed
results do not automatically reach exception middleware. Keep validation-result
inspection and error-to-HTTP mapping in the centralized boundary, with supported
exceptions propagating to middleware.

Controllers translate HTTP input and call DevFlow.SharedKernel.Cqrs.ISender.
They must not inject DbContext, repositories, identity managers, or domain services.
Inspect the shared ApiController, PublicApiController, or IntegrationApiController
before choosing a base; protected bases require their authentication dependencies.
Use permission attributes only with the required authentication and authorization
registration; do not invent permission constants or make a protected route public.

Use DevFlow.SharedKernel.Abstractions.Message ICommand, IQuery, ICommandHandler,
IQueryHandler, and IValidator; use Result/Result<T> from Abstractions.Shared.
Do not add MediatR or a mapping library. Prefer sealed records for messages and
sealed classes for handlers/validators. Read interface signatures before coding.
Pass CancellationToken through asynchronous calls. Validate meaningful input;
IValidator.ValidateAsync throws CqrsValidationException for invalid requests.

AddTasksApplication scans its assembly and registers behaviors in this order:
validation, logging, performance, tracing, transaction. Preserve this order.
Before resolving requests, ensure Infrastructure supplies IUnitOfWork:
TransactionPipelineBehavior requires it in its constructor even for queries,
although only commands execute a transaction. Do not register a dummy unit of
work to hide missing persistence. A real unit of work must own transaction and
commit semantics. Handlers and repositories do not call SaveChangesAsync.

## Persistence and contracts

Application defines persistence/integration interfaces; Infrastructure implements
them. Domain depends only on BuildingBlocks. Application never references
Infrastructure or EF contexts. Keep DTOs separate from EF entities and reserve
cross-module Contracts for actual cross-module boundaries.

For EF persistence, inspect EfRepositoryBase<TEntity,TId,TDbContext> in
DevFlow.EntityFrameworkCore.Repositories and IRepositoryBase<TEntity> in SharedKernel.
The imported EF base is a helper and does not implement the repository interface
automatically. Query() is no-tracking; TrackedQuery() explicitly tracks.
Implement the required interface members and register the concrete repository.
Do not assume RepositoryBase, ReadRepositoryBase, SqlConnectionFactory, or a
module DbContext exists. Introduce only persistence needed for the feature.
Dapper is optional: follow an existing relevant pattern or an explicit request;
do not create a parallel read repository merely for symmetry.
Parameterize SQL values and whitelist sort columns if SQL is needed.

Use existing failure mapping where available, let domain exceptions reach
GlobalExceptionHandlingMiddleware, and document response status types.
CreatedAtAction must refer to an implemented route.

## Verification

Follow any explicit verification constraints. Otherwise build the affected
projects, run scripts/Test-Architecture.ps1, and use focused behavior tests when
they add confidence. Check every changed command/query against the linked layout:
each use case has its own Commands/Queries folder, each type has its own matching
file, and namespaces follow the folders. Inspect the solution for existing test
projects and run the relevant tests; add tests only for meaningful behavior.
For HTTP changes, verify request models are in API `Contracts/<Area>/` files,
controller files contain only controllers, and actions contain neither
`IValidationResult` checks nor application-error-to-HTTP mapping. Exercise invalid
input and handler failures through the HTTP boundary to verify centralized
statuses and envelopes, as well as the successful response.
Report the endpoint, major changes, persistence/access assumptions, and checks
actually run. Do not claim database or authentication readiness without evidence.
