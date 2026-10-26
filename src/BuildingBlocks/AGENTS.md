# BuildingBlocks rules

Root `AGENTS.md` applies here.

BuildingBlocks are reusable technical primitives, not a home for module business
logic. They may depend on other BuildingBlocks only when the direction is one-way
and cycle-free; they must never reference a module.

## Project responsibilities

| Project | Responsibility |
| --- | --- |
| `DevFlow.SharedKernel` | Domain bases, results/errors, integration result contracts, custom CQRS, validation, security abstractions, and repository contracts |
| `DevFlow.AspNetCore` | HTTP controllers, middleware, authentication/authorization helpers, API envelopes, web-host registration, and outbound HTTP integrations under HttpClient/ |
| `DevFlow.EntityFrameworkCore` | Generic EF repositories, auditing, and local reliability persistence primitives |
| `DevFlow.Observability` | OpenTelemetry registration and privacy processors |
| `DevFlow.ServiceDefaults` | Shared host defaults, service discovery, resilience, health checks, and observability wiring |

Keep abstractions provider-neutral unless the project explicitly represents a
provider integration. Do not add a shared primitive for a single caller merely to
avoid placing it in its owning module.

Changes to CQRS interfaces or pipeline behavior affect every module. Preserve
request cancellation, behavior ordering, result semantics, and command-only
transaction behavior. `TransactionPipelineBehavior` resolves `IUnitOfWork`; a host
that registers the pipeline must provide a real implementation before sending
requests.

Shared persistence types contain technical records only. Business entities and
module-specific repositories stay in their module. HTTP and observability code must
not record secrets, tokens, raw sensitive bodies, or unreviewed URL/query data.

When changing a public primitive, search all consumers and build the affected
BuildingBlocks and modules. Run the architecture script for reference changes.

Application projects must not reference DevFlow.AspNetCore. Integration interfaces
use DevFlow.SharedKernel.Integrations results; HTTP adapters in Infrastructure
inject the destination-specific IIntegrationHttpClient<TDestination>.

