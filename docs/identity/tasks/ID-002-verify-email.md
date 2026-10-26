---
TaskId: ID-002
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/email-verifications/confirm"
---

# ID-002 - Verify email

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/email-verifications/confirm`

**User story:** US-002: As a new account holder, I can verify my email address before logging in.

**Actor:** Anonymous

**Authentication/authorization:** Public; token-specific throttling

**Dependencies:** ID-001

## Request contract

**Path/query/header:** None

**Body/DTO:** `ConfirmEmailRequest(string Token)`

**JSON example:**

```json
{
  "token": "<opaque-verification-token>"
}
```

## Response contract

204 No Content: no response body

**Expected failures:** 400 malformed, 410 invalid/expired/used token, 403 suspended/deleted.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Token required, opaque base64url format, bounded length 32..512.
2. Do not accept token through URL or store plaintext; hash it before lookup.

## Business logic

1. Atomically locate purpose=verify_email token by SHA-256 digest, unconsumed and unexpired; lock row or compare-and-set consumed timestamp.
2. Mark token consumed, set User.EmailVerifiedAt and User.Status=active only when pending; never unsuspend a suspended/deleted account.
3. Reject replay and report a generic expired/invalid token response; audit account verification.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ConfirmEmail command, validator and transactional repository update.
4. Use concurrency-safe token consumption and avoid showing token or email in logs.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. First valid verification succeeds and activates pending user.
2. Second attempt with same token fails; expired/incorrect purpose token fails.
3. Suspended/deleted user is not activated.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
