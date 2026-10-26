---
TaskId: ID-032
Status: Pending
Priority: P0
Phase: "D Access control"
Method: GET
Route: "/api/v1/access/permissions"
---

# ID-032 - List permission catalog

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/access/permissions`

**User story:** US-032: As an authorized admin, I can browse available permissions when creating a direct user exception.

**Actor:** Organization admin or platform admin

**Authentication/authorization:** organization.permissions.manage or platform role as appropriate

**Dependencies:** ID-030; permission seed

## Request contract

**Path/query/header:** scopeKind: organization|project|team|performance|platform (required); module?: string; tenantId?: Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<PermissionCatalogResponse> where PermissionCatalogResponse(Guid Id, string Code, string Module, string ScopeKind, string Description, bool IsSensitive)

**Expected failures:** 400 filters, 403 unauthorized.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. ScopeKind known enum; module <=40 matching registered module codes; tenantId for tenant-scoped permissions.
2. Return only active permissions that actor is entitled to consider delegating.

## Business logic

1. Filter seeded shared.Permissions by Module, ScopeKind and safety/actor authority; avoid returning platform-sensitive definitions to tenant admins.
2. Separate permission catalog visibility from permission grant authority.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListPermissions query with filtered projection.
4. Use existing access policy service; do not add a permission administration endpoint.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Tenant user sees only eligible tenant codes; disabled permissions omitted.
2. Request with platform scope from tenant actor denied.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
