---
TaskId: ID-038
Status: Pending
Priority: P0
Phase: "D Access control"
Method: DELETE
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/permissions/{grantId}"
---

# ID-038 - Revoke direct user permission grant

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `DELETE /api/v1/organizations/{tenantId}/members/{memberId}/permissions/{grantId}`

**User story:** US-038: As an administrator, I can withdraw a one-off permission without changing the employee's base role.

**Actor:** Authorized organization/scope admin

**Authentication/authorization:** permission revocation delegation policy

**Dependencies:** ID-037

## Request contract

**Path/query/header:** tenantId, memberId, grantId Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 403 unauthorized, 404 foreign/nonexistent.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Grant belongs to specified member and tenant; idempotent if already revoked.
2. Actor must have authority over exact scope and permission, not only target user.

## Business logic

1. Set RevokedAt, retain historical grant and invalidate cache immediately.
2. Audit action; no modification to shared.RolePermissions or role assignments.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokeMemberPermission command/handler and composite key lookup.
4. Reuse existing grant audit and authorization policy.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Direct exception disappears from effective access; role-derived access unaffected.
2. Foreign grant ID returns 404, unauthorized actor denied.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
