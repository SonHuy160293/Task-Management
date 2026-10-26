---
TaskId: ID-031
Status: Pending
Priority: P1
Phase: "D Access control"
Method: GET
Route: "/api/v1/access/roles/{roleId}"
---

# ID-031 - Get role details and permissions

**Status: Pending** | **Priority: P1** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/access/roles/{roleId}`

**User story:** US-031: As an administrator, I can understand exactly which permissions a built-in role confers.

**Actor:** Organization admin or platform admin

**Authentication/authorization:** Same role catalog visibility constraints as ID-030

**Dependencies:** ID-030

## Request contract

**Path/query/header:** roleId: Guid; query: tenantId?: Guid for non-platform roles

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: RoleDetailResponse(Guid Id, string Code, string DisplayName, string ScopeKind, string? Description, IReadOnlyList<PermissionCatalogResponse> Permissions)

**Expected failures:** 400 ID, 403 actor, 404 invisible/inactive role.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. roleId UUID valid; role active and visible to requester.
2. Do not expose inactive sensitive platform roles to non-platform admins.

## Business logic

1. Load shared.RolePermissions matching selected shared.Role and active shared.Permissions; preserve scope kind check.
2. Return only static catalog grants; do not confuse with member effective permissions.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetRoleDetail query and optimized join/projection.
4. Reuse authorization check from catalog.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Permissions correspond to role scope and seeded mappings.
2. Unavailable role returns 404; platform-only role not discoverable by tenant admin.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
