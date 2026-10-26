---
name: create-ef-entity
description: Create or update a DevFlow domain entity, EF Core mapping, and module DbContext registration from requested fields or a supplied schema.
---

# Create a DevFlow EF entity

Read ARCHITECTURE.md and applicable AGENTS.md files if present. Inspect the target
module and shared entity types before choosing inheritance or conventions.
The Tasks starter currently has no entities, DbContext, provider, or database
connection. Do not assume a reference Booking module exists.

Resolve module, entity, key type, fields, nullability, lengths, precision, indexes,
relationships, and schema from user-provided DDL or existing mappings. Prefer
supplied schema over guesses. Ask only about material unresolved requirements.

## Domain and mapping

Place domain models under:
src/Modules/DevFlow.<Module>/DevFlow.<Module>.Domain/Models/<Entity>.cs

Use namespace DevFlow.<Module>.Domain.Models. Inspect Entity<TId>, Entity,
SoftDeletableEntity, and audit interfaces in DevFlow.SharedKernel.Abstractions.Domain.
Choose only the base and audit/soft-delete behavior the entity needs.
Keep Domain free of EF attributes, DbContext, and Infrastructure references.
Preserve business invariants through constructors/factories and domain methods.

Place IEntityTypeConfiguration<Entity> implementations under the module's
Infrastructure/Persistence/Configurations folder. Use plural PascalCase table
names and PascalCase columns. Preserve an established module schema; for a new
module, establish the schema deliberately rather than copying cinema, ordering,
or payment. Quote PascalCase identifiers in PostgreSQL SQL.

Map key generation, required fields, lengths, precision, relationships, indexes,
delete behavior, and concurrency only when supported by requirements.
Avoid duplicating audit/soft-delete conventions already supplied by mappings or
interceptors. Business rules belong in Domain, not EF configuration.
Changing a mapping does not rename a deployed table: do not rewrite migration
history or snapshots to simulate a schema change.

## DbContext and runtime

Reuse the module DbContext when present. Add the DbSet and ensure configuration
discovery; avoid duplicate manual registration when assembly scanning exists.
If the context does not exist, add it only when entity persistence is in scope.
Select a provider from the user's requirements or established configuration;
optional SQL Server and PostgreSQL Compose services do not choose one for the API.

Register required infrastructure through AddTasksInfrastructure (or the target
module equivalent). If the request also introduces runtime requests, implement
and register IUnitOfWork so the CQRS transaction pipeline can resolve and commit.
Do not add SaveChangesAsync calls to handlers or repositories.

Do not create migrations, apply schema changes, add repositories, DTOs, controllers,
or unrelated features unless requested or necessary for the authorized work.
Report any schema ambiguity rather than inventing database constraints.

## Verification

Review model, mapping, and DbContext together against the supplied fields:
types, nullability, keys, relationships, names, lengths, precision, and indexes.
Follow explicit verification constraints; otherwise build affected projects and
run scripts/Test-Architecture.ps1. Use meaningful mapping tests when warranted.
Do not create a migration merely to validate a model.
Report files changed, unresolved schema choices, and checks actually performed.