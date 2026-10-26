---
TaskId: ID-047
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: POST
Route: "/api/v1/platform/users/{userId}/roles"
---

# ID-047 - Grant platform role

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `POST /api/v1/platform/users/{userId}/roles`

**User story:** US-047: As a top-level security operator, I can assign a limited platform role to trusted staff.

**Actor:** Privileged platform security admin

**Authentication/authorization:** platform.roles.assign; cannot self-grant or escalate to unavailable root role

**Dependencies:** Secure platform admin bootstrap + role/permission catalog, ID-041

## Request contract

**Path/query/header:** None

**Body/DTO:** `AssignPlatformRoleRequest(Guid RoleId, string Reason); route userId Guid`

**JSON example:**

```json
{
  "roleId": "00000000-0000-7000-8000-000000000004",
  "reason": "On-call support duty approved"
}
```

## Response contract

201 Created: PlatformRoleAssignmentResponse(Guid Id, Guid UserId, Guid RoleId, string RoleCode, DateTimeOffset AssignedAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 invalid role/reason, 403 escalation, 404 target, 409 duplicate.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Target user active/verified, RoleId valid and role ScopeKind=platform, IsActive=true, IsAssignable=true.
2. Reason trimmed 8..300 for audited operator justification (not stored in assignment row; record in security audit).
3. Actor cannot assign self, cannot bootstrap unrestricted root, cannot grant role exceeding their delegation policy.

## Business logic

1. Enforce privileged-role grant policy and optionally two-person approval outside V1 if required by deployment.
2. Create shared.PlatformRoleAssignment with AssignedByUserId, AssignedAt and auditable reason; protect partial-unique active role pair.
3. Use seed/bootstrap to create initial platform root out of band, never unauthenticated API.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement AssignPlatformRole command with global role scope checks and platform audit.
4. Handle concurrent duplicates and revoked-before-regrant behavior.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Valid platform assignment recorded and reflected in platform authorization only.
2. Tenant admin cannot assign platform roles, platform actor cannot self-escalate.
3. Duplicate active role is not created.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
