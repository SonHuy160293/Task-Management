---
TaskId: ID-039
Status: Pending
Priority: P0
Phase: "D Access control"
Method: GET
Route: "/api/v1/organizations/{tenantId}/me/effective-permissions"
---

# ID-039 - Get my effective permissions at scope

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/me/effective-permissions`

**User story:** US-039: As a user or frontend, I can know which actions I may attempt in one selected organization/project/team scope.

**Actor:** Active tenant member

**Authentication/authorization:** Self only, active tenant/membership/scope

**Dependencies:** ID-034, ID-037, complete auth policy and RLS-safe read

## Request contract

**Path/query/header:** scopeId: Guid (required)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: EffectivePermissionsResponse(Guid TenantId, Guid ScopeId, string ScopeKind, IReadOnlyList<string> PermissionCodes, DateTimeOffset EvaluatedAt)

**Expected failures:** 400 UUID, 401, 403 suspension, 404 foreign scope.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. scopeId UUID valid, belongs to tenant, status=active.
2. No caller memberId or userId accepted; membership must be active; suspended global user/tenant denied.

## Business logic

1. Compute active role permissions UNION active direct grants at this exact scope only; filter revoked/expired/disabled role and permission.
2. Apply hard guest restrictions and deny-by-default; do not inherit organization scope to project scope implicitly.
3. Return UI hints only: each protected endpoint and owning business module must reauthorize server-side on every call.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetMyEffectivePermissions query using shared authorization service and indexed joins.
4. Use injected clock and invalidation-capable cache if any; evaluate no cross-tenant IDs.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Developer role plus extra direct project grant appears in list only for specified project.
2. Expired or revoked entries disappear; suspended member cannot read effective list.
3. Guest cannot see sensitive performance codes even if corrupt grant exists.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
