---
TaskId: ID-045
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: POST
Route: "/api/v1/platform/users/{userId}/reactivate"
---

# ID-045 - Reactivate global user

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `POST /api/v1/platform/users/{userId}/reactivate`

**User story:** US-045: As platform security, I can restore a legitimate account without granting new sessions.

**Actor:** Platform security admin

**Authentication/authorization:** platform.users.suspend or explicit platform.users.reactivate

**Dependencies:** ID-044

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangeGlobalUserStatusRequest(string Reason); route userId Guid`

**JSON example:**

```json
{
  "reason": "Ownership verified after remediation"
}
```

## Response contract

204 No Content

**Expected failures:** 403 unauthorized, 404 user, 409 ineligible state.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. User is suspended and not deleted; reason trimmed 8..500.
2. If email unverified require verification before login; do not trust existing revoked sessions.

## Business logic

1. Set shared.Users.Status=active only for verified user; leave AuthSessions revoked, require fresh login.
2. Membership and tenant suspensions remain intact; write platform audit.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ReactivateGlobalUser command with strict state transition.
4. Invalidate global-user status cache.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Reactivated user can log in fresh but cannot reuse revoked refresh secret.
2. Suspended tenant memberships remain denied; deleted status cannot be restored.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
