---
TaskId: ID-023
Status: Pending
Priority: P1
Phase: "B Organizations"
Method: DELETE
Route: "/api/v1/organizations/{tenantId}/members/{memberId}"
---

# ID-023 - Remove organization member

**Status: Pending** | **Priority: P1** | **Phase: B Organizations**

**Endpoint:** `DELETE /api/v1/organizations/{tenantId}/members/{memberId}`

**User story:** US-023: As an organization owner/admin, I can offboard an employee while preserving audit history.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.members.manage; last-owner guard

**Dependencies:** ID-021, ID-035, ID-038

## Request contract

**Path/query/header:** tenantId: Guid, memberId: Guid (UUIDv7)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 403 self/owner protection, 404 foreign, 409 last active owner.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Cannot remove self or last active owner in V1; admins may not remove owner.
2. Member belongs to tenant; repeat removal is idempotent.

## Business logic

1. Atomically mark membership removed, RemovedAt=now, revoke all live role assignments and direct grants; DO NOT delete shared.User or credentials.
2. Invalidate authorization context and audit; historical work item attribution must remain valid.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RemoveMember command, bulk-grant revocation in same transaction and last-owner lock.
4. Do not cascade-delete shared.Users or other modules data.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Offboarded user loses only this tenant, not access to other tenants.
2. Historical audit rows remain; owner safety and foreign IDs guarded.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
