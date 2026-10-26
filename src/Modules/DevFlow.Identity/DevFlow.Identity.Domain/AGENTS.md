# Identity domain model rules

Root and Identity module `AGENTS.md` files apply here.

The models in `Models/` mirror a deliberate PostgreSQL identity/RBAC design. Before
editing one, inspect its matching EF configuration and the corresponding table in
`docs/identity/identity_erd_postgresql18.md`.

## Model construction

- Keep EF attributes, SQL, `DbContext`, and ASP.NET Core Identity types out of the
  Domain project.
- Preserve private setters and the private parameterless constructor needed for EF.
  Add public factories or domain methods when real behavior is introduced instead
  of exposing unrestricted setters.

## Identity and auditing

- Existing Identity models use UUIDv7-compatible `Guid` identifiers; preserve
  those key types. Use UTC `DateTimeOffset` values for persisted instants.
- For an entity that does not need the shared audit contract, implement
  `DevFlow.SharedKernel.Abstractions.Domain.IEntity<TId>` directly. Choose `TId`
  to match the model's actual key type, such as `Guid`, `int`, `long`, or `string`,
  and keep the EF mapping and repository key types aligned. Preserve existing
  Identity UUID keys; support for other types is not a reason to change them.
- The non-generic `IEntity` specializes `IEntity<Guid>`. Use `IEntity<TId>` for
  other key types. A class implements an interface; it inherits a base class.
- `IEntity<TId>` requires only a readable `Id` property. Keep its setter private
  and initialize the key according to the model's generation strategy. The
  interface does not add audit fields or enable automatic auditing; retain only
  the timestamps required by the model, such as `IssuedAt` or `OccurredAt`.
- Inherit `Entity<TId>` only when the shared `IAuditableEntity` contract matches
  the table; use `Entity<Guid>` for the existing Identity models that meet that
  requirement. Several identity records intentionally use standalone timestamps;
  do not normalize them into an audited base class mechanically. Implementing
  `IEntity<TId>` does not make append-only security audit records mutable.

## Relationships and sensitive state

- Preserve navigation/scalar pairs required by Infrastructure mappings. Domain
  navigations remain persistence-ignorant and must not cross module boundaries.
- Keep status, scope-kind, purpose, and event-code values aligned with database
  constraints. Changing strings in isolation can make the model and schema disagree.
- Token and credential state contains hashes only. Avoid `ToString`, equality, or
  diagnostics that could reveal sensitive byte arrays or hashes.

## Persistence coordination

When changing a model, update its Infrastructure mapping and `IdentityDbContext`
`DbSet` registration as needed. Check indexes, relationships, constraints, defaults,
delete behaviors, nullability, and identifier generation against the schema design,
then build the affected project. Keep all EF configuration in Infrastructure.

