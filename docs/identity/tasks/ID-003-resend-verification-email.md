---
TaskId: ID-003
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/email-verifications/resend"
---

# ID-003 - Resend verification email

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/email-verifications/resend`

**User story:** US-003: As an unverified user, I can request another verification message without revealing whether an account exists.

**Actor:** Anonymous

**Authentication/authorization:** Public; rate-limited

**Dependencies:** ID-001, ID-002

## Request contract

**Path/query/header:** None

**Body/DTO:** `ResendVerificationRequest(string Email)`

**JSON example:**

```json
{
  "email": "alice@example.com"
}
```

## Response contract

202 Accepted: GenericMessageResponse(string Message)

**Expected failures:** 400 invalid email, 429 throttle, 503 temporary delivery failure handled without enumeration.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Email format/length <=320; IP/email throttling and cooldown.
2. Never reveal account state.

## Business logic

1. For pending, unverified user create a fresh hashed verify_email token and invalidate outstanding purpose-equivalent tokens; enqueue email safely.
2. For active/unknown/suspended accounts, perform no visible action and return same generic response; keep timing comparable.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement CQRS command and token invalidation/issuance method.
4. Reuse secure mail and rate-limit services from registration, without duplicate token utilities.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Unverified user gets fresh single-use token and older token fails.
2. Known and unknown emails receive same public response.
3. Repeated requests are throttled or cooldown-limited.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
