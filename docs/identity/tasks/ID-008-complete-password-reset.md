---
TaskId: ID-008
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/password-resets/confirm"
---

# ID-008 - Complete password reset

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/password-resets/confirm`

**User story:** US-008: As a user, I can safely replace a forgotten password with a valid recovery token.

**Actor:** Anonymous

**Authentication/authorization:** Public; token-throttled

**Dependencies:** ID-004, ID-007

## Request contract

**Path/query/header:** None

**Body/DTO:** `ResetPasswordRequest(string Token, string NewPassword)`

**JSON example:**

```json
{
  "token": "<opaque-reset-token>",
  "newPassword": "a new long unique passphrase 2026"
}
```

## Response contract

204 No Content

**Expected failures:** 400 invalid input, 410 invalid/used/expired token, 403 disallowed account.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Opaque token required length 32..512 and correct purpose, unconsumed, unexpired.
2. NewPassword >=15 chars; support >=64 chars, common/breached-password blocklist; must not log secrets.

## Business logic

1. Lock/consume valid token once; update Argon2id hash and PasswordChangedAt; reset FailedAttempts/LockedUntil.
2. Revoke all existing AuthSessions and RefreshTokens for user after successful password change; leave global suspension intact.
3. Record security audit, send security notification through existing delivery integration.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement CQRS ResetPassword command and transactional one-time-token/password/session update.
4. Guarantee either all security changes commit or token remains consumable after a failed transaction.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Used/expired token cannot reset again; wrong-purpose token denied.
2. Prior sessions cannot refresh; new password authenticates and previous password fails.
3. User status stays suspended if it was suspended.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
