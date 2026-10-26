---
TaskId: ID-048
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: DELETE
Route: "/api/v1/platform/users/{userId}/roles/{assignmentId}"
---

# ID-048 - Revoke platform role

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `DELETE /api/v1/platform/users/{userId}/roles/{assignmentId}`

**User story:** US-048: As a platform operator, I can withdraw privileges from another operator.

**Actor:** Privileged platform security admin

**Authentication/authorization:** platform.roles.assign; last-platform-admin safeguard

**Dependencies:** ID-047

## Request contract

**Path/query/header:** userId and assignmentId Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 403 forbidden/last operator, 404 missing assignment, 409 protected final operator.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Assignment must belong to user; idempotent for already revoked.
2. No self-revoke in V1; cannot remove final active platform recovery/admin role.

## Business logic

1. Atomically set RevokedAt and append platform audit; prevent concurrent last-operator revocations.
2. Invalidate platform privilege cache immediately; do not affect organization assignments.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokePlatformRole command with last-operator lock and audit.
4. Use existing permission check and replay-safe state transition.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Role is no longer effective after revoke; tenant-specific permissions unchanged.
2. Two concurrent requests cannot remove all platform admins; outsider denied.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
