---
TaskId: ID-006
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/logout"
---

# ID-006 - Log out current session

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/logout`

**User story:** US-006: As a user, I can terminate my current device session.

**Actor:** Authenticated user

**Authentication/authorization:** Bearer JWT + session validation; origin check for cookie deletion

**Dependencies:** ID-004, ID-005

## Request contract

**Path/query/header:** None

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content: expires refresh cookie

**Expected failures:** 401 missing/invalid authentication (or 204 if endpoint deliberately tolerates expired session with valid cookie).

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Identify acting user/session from validated claims, not request body.
2. A repeated logout should be idempotent.

## Business logic

1. Revoke current AuthSession and outstanding refresh tokens; invalidate authorization of corresponding session.
2. Expire refresh cookie; emit security audit event.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement Logout command and session revocation repository routine.
4. Use established cookie writer; do not trust client-provided session IDs.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Current session cannot refresh after logout; another device remains valid.
2. Repeated logout returns 204; cookie deletion flags match issuance.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
