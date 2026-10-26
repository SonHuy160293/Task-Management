---
TaskId: ID-044
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: POST
Route: "/api/v1/platform/users/{userId}/suspend"
---

# ID-044 - Suspend global user

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `POST /api/v1/platform/users/{userId}/suspend`

**User story:** US-044: As platform security, I can disable a compromised global account across every organization.

**Actor:** Platform security admin

**Authentication/authorization:** platform.users.suspend; cannot suspend last platform recovery operator

**Dependencies:** ID-004, ID-006, ID-047 (or secured preseeded platform administrators)

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangeGlobalUserStatusRequest(string Reason); route userId Guid`

**JSON example:**

```json
{
  "reason": "Account compromise confirmed by security team"
}
```

## Response contract

204 No Content

**Expected failures:** 400 reason, 403 protection, 404 user, 409 already suspended.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reason 8..500; userId valid and user active; self-suspension prohibited.
2. Protect last active platform admin/recovery account from lockout.

## Business logic

1. Update shared.Users.Status=suspended, revoke all AuthSessions/RefreshTokens; do not delete tenant membership records.
2. Append shared.PlatformSecurityAuditEvent; block future login and all access immediately.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement SuspendGlobalUser command, global status gate and session revocation.
4. Atomic safety guard for platform administrative access.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. All org access and refresh tokens for user denied after suspension.
2. Other accounts unaffected; last platform operator protection works concurrently.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
