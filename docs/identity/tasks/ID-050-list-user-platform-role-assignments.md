---
TaskId: ID-050
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: GET
Route: "/api/v1/platform/users/{userId}/roles"
---

# ID-050 - List user platform role assignments

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `GET /api/v1/platform/users/{userId}/roles`

**User story:** US-050: As a platform administrator, I can inspect active/revoked role assignments before changing privileged access.

**Actor:** Platform security admin

**Authentication/authorization:** platform.roles.read or platform.roles.assign; never tenant roles

**Dependencies:** ID-049; seeded platform.roles.read or platform.roles.assign

## Request contract

**Path/query/header:** userId Guid; query: includeRevoked?: bool=false

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<PlatformRoleAssignmentResponse> where PlatformRoleAssignmentResponse(Guid Id, Guid UserId, Guid RoleId, string RoleCode, DateTimeOffset AssignedAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 UUID, 403 unauthorized, 404 user.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. UserId UUID valid; user exists; includeRevoked only for privileged audit-capable admin.
2. Return only ScopeKind=platform role assignments.

## Business logic

1. Join shared.PlatformRoleAssignments to shared.Roles by RoleId and ScopeKind=platform, filter by target user.
2. No tenant-owned roles or direct grants appear; sort AssignedAt descending.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListPlatformRoleAssignments query and permission-checking handler.
4. Use a no-tracking projection and avoid leaking global security metadata to tenant roles.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Admin can inspect role IDs and use returned assignmentId for future revocation.
2. Tenant owner cannot enumerate platform roles; revoked rows excluded unless authorized.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
