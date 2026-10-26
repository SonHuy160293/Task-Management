# DevFlow Architecture

DevFlow is a .NET 10 modular monolith using ASP.NET Core and Clean Architecture.
Business capabilities live under src/Modules/<ModuleName>; cross-module contracts
belong in src/Contracts and shared technical primitives in src/BuildingBlocks.

## Dependency direction

- Gateway -> BuildingBlocks only.
- API -> Application, Infrastructure, Contracts, BuildingBlocks.
- Worker executable -> own Application, Infrastructure, Contracts, BuildingBlocks.
- MigrationRunner -> owning module Infrastructure, BuildingBlocks.
- Infrastructure -> Application, Domain, Contracts, BuildingBlocks.
- Application -> Domain, Contracts, BuildingBlocks.
- Domain -> BuildingBlocks only.
- Contracts -> BuildingBlocks only when needed.
- BuildingBlocks -> other BuildingBlocks without cycles.

Application must never reference Infrastructure or DevFlow.AspNetCore. Cross-module domain and
infrastructure references are forbidden; communicate through contracts.

## API and CQRS

Controllers translate transport input into commands and queries sent through
DevFlow.SharedKernel.Cqrs.ISender. They must not inject DbContext, identity managers,
repositories, or business services. Use the imported custom mediator, not MediatR.

Application commands and queries follow the
[Modules CQRS file layout](src/Modules/AGENTS.md#cqrs-file-layout), using
Registration and GetUser as the reference implementations.

HTTP request models and centralized failure propagation follow the
[Modules API conventions](src/Modules/AGENTS.md#api-request-contracts-and-failure-handling).

Preserve the pipeline order:
Controller -> ISender.SendAsync -> ValidationDefaultBehavior -> LoggingBehavior
-> PerformancePipelineBehavior -> TracingPipelineBehavior
-> TransactionPipelineBehavior -> Handler -> Result / Result<T>.

The transaction behavior uses IUnitOfWork for commands only. Handlers and
repositories must not call SaveChangesAsync; the pipeline commits once after
successful completion. Queries do not open command transactions.
DomainException errors reach GlobalExceptionHandlingMiddleware and its ApiResponse
envelope. Avoid broad controller try/catch blocks.

## Persistence and runtime

Each module owns its persistence. Use plural PascalCase tables and PascalCase
columns within module schemas; quote PostgreSQL identifiers. Cross-module IDs
are scalar references; do not span databases with local transactions.
Shared outbox/inbox/idempotency persistence belongs to BuildingBlocks.EntityFrameworkCore.
Business integration abstractions belong to Application, typed HTTP implementations
to Infrastructure. Shared guards contain no EF, HTTP, or module dependencies.

Reusable outbound HTTP behavior belongs to DevFlow.AspNetCore/HttpClient, organized
by clients, handlers, options, correlation, serialization, validation, registration,
and observability. Infrastructure adapters inject IIntegrationHttpClient<TDestination>.
The factory creates named transports with isolated credentials and resilience
pipelines. IntegrationResult types live in DevFlow.SharedKernel/Integrations so
Application contracts do not depend on ASP.NET Core.

DevFlow.Gateway is an independent YARP host referencing BuildingBlocks only. Frontend
HTTP goes through external TLS ingress and explicit gateway routes. Internal HTTP
uses unique API network aliases; workers/RabbitMQ retain asynchronous integration.
Production APIs publish no host ports. Gateway readiness excludes backend availability.
See [ADR 0005](docs/decisions/0005-yarp-api-gateway.md) and
[gateway operations](docs/gateway/README.md).

The starter Tasks module has API, Application, Domain, and Infrastructure projects.
No business models, database provider, authentication policy, workers, or
migration runner are implemented yet. Register a real IUnitOfWork before introducing
requests that resolve the transaction pipeline. Health endpoints currently check
host health only. Add other runtime projects when requirements justify them.

## Changes and enforcement

DevFlow.Identity has API, Application, Domain, and Infrastructure projects, with
eighteen domain models and PostgreSQL mappings in IdentityDbContext. Application
owns security and registration contracts and local password screening; Infrastructure
supplies Argon2id hashing, verification-token generation, Redis registration limits,
the Identity EF Unit of Work, and registration persistence. The API hosts the public
`POST /api/v1/auth/register` endpoint and its registration-protection middleware.
See docs/identity/SECURITY_SETUP.md for deployment requirements. Registration stages an encrypted verification email in the Identity outbox within the account transaction. Identity.MigrationRunner is a one-shot host that registers
persistence only and applies Infrastructure's EF migrations. InitialIdentity
creates the eighteen tables and three case-insensitive expression indexes on
PostgreSQL 18 without seed data. The supported Identity deployment waits for
PostgreSQL health, migration success, runtime grants, and Redis health before
starting the API. Its HTTP host and worker use the same Identity database.
APIs never apply migrations at startup. See docs/identity/MIGRATIONS.md.
The deployment separates administrative, migration, API, and worker credentials.
Tenant RLS/session settings and production retention remain separate operational work.

Notification owns its own PostgreSQL database, durable email jobs, provider callbacks,
suppression and templates. Identity.Worker and Notification.Worker are independent
executables; DevFlow.Messaging supplies MassTransit 8.5.8/RabbitMQ transport and the
existing outbox relay. Cross-module integration uses versioned encrypted contracts
and durable receipt/inbox deduplication. Delivery is at least once.
See docs/decisions/0003-durable-notification-delivery.md and docs/notification/README.md.

Deployment layout is governed by
[ADR 0004](docs/decisions/0004-module-oriented-docker-deployment.md). The shared
platform, Identity, and Notification are separate Compose projects behind the
Windows/Linux onboarding interface in `scripts/devflow.*`. Local build overrides
and image-only server definitions share the same module model. The server template
requires external trusted TLS RabbitMQ and TLS ingress before public traffic.
See [deployment operations](deploy/README.md); the earlier isolated learning stacks
are legacy examples and are not the supported onboarding path.

Run scripts/Test-Architecture.ps1 to enforce project dependency direction and cycles.
For changes to durable boundaries, inspect implementation and tests, document
the decision under docs/decisions, update this map, and extend mechanical checks.

Adapted from the supplied reference ARCHITECTURE.md. Booking-specific modules and
database choices are not requirements for DevFlow.
