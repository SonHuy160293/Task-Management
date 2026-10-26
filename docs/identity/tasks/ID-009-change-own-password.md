---
TaskId: ID-009
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/me/password/change"
---

# ID-009 - Change own password

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/me/password/change`

**User story:** US-009: As a signed-in employee, I can change my password after proving I know the current one.

**Actor:** Authenticated user

**Authentication/authorization:** Self only; validated session

**Dependencies:** ID-004, ID-006

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangePasswordRequest(string CurrentPassword, string NewPassword)`

**JSON example:**

```json
{
  "currentPassword": "my current unique passphrase",
  "newPassword": "another new unique passphrase 2026"
}
```

## Response contract

204 No Content; caller must sign in again

**Expected failures:** 400 weak/same password, 401 wrong current password, 403 inactive/SSO-only.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. CurrentPassword nonempty; NewPassword >=15, support >=64, blocklist checked; reject identical current/new value.
2. Authenticate current password and confirm account supports password auth.

## Business logic

1. Verify existing Argon2id hash; update hash and PasswordChangedAt transactionally.
2. Revoke ALL sessions including current and send security notification; no alternate user ID is accepted.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ChangePassword command with password reauthentication and session revocation.
4. Ensure calling session can finish request before revocation takes effect.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Wrong current password returns 401/400 without change; successful change revokes all sessions.
2. New password succeeds; prior password/token fails; audit excludes secret fields.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
