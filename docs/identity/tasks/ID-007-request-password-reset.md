---
TaskId: ID-007
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/password-resets/request"
---

# ID-007 - Request password reset

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/password-resets/request`

**User story:** US-007: As a user who forgot my password, I can request a recovery link privately.

**Actor:** Anonymous

**Authentication/authorization:** Public; rate-limited

**Dependencies:** ID-001, mail integration

## Request contract

**Path/query/header:** None

**Body/DTO:** `RequestPasswordResetRequest(string Email)`

**JSON example:**

```json
{
  "email": "alice@example.com"
}
```

## Response contract

202 Accepted: GenericMessageResponse(string Message)

**Expected failures:** 400 invalid request, 429 throttle.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Validate email format and <=320 characters; apply per-account/IP throttling and anti-automation controls.
2. Respond identically for unknown and known accounts.

## Business logic

1. For eligible password-authenticated active user create hashed reset_password one-time token, invalidate old unused resets and enqueue email.
2. Do not change account status, password, or sessions merely because reset was requested; SSO-only/unknown users get generic response.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement command, validator and secure one-time token issuance.
4. Use existing email delivery contract and neutral timing strategy.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Known eligible account receives reset instructions; unknown account receives same response.
2. Stored token hashed, expiring and single-use; throttling enforced.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
