---
TaskId: ID-013
Status: Pending
Priority: P1
Phase: "A Authentication"
Method: DELETE
Route: "/api/v1/me/sessions/{sessionId}"
---

# ID-013 - Revoke one of my sessions

**Status: Pending** | **Priority: P1** | **Phase: A Authentication**

**Endpoint:** `DELETE /api/v1/me/sessions/{sessionId}`

**User story:** US-013: As a user, I can remotely sign out one device.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-012, ID-006

## Request contract

**Path/query/header:** sessionId: Guid (UUIDv7)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 400 malformed ID, 404 foreign/nonexistent session, 401 unauthenticated.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. sessionId must parse as UUID; it must belong to caller.
2. Request is idempotent for already revoked owned session.

## Business logic

1. Find session by user and ID without permitting IDOR across user accounts; revoke session and its unexpired refresh tokens.
2. Invalidate session and audit device revocation; clear cookie only if sessionId equals current sid.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokeMySession command and ownership-constrained repository method.
4. Use same centralized revocation primitive as logout.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. User can revoke own session, then its refresh fails.
2. Foreign session ID returns 404 without revealing existence.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
