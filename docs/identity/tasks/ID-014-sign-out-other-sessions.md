---
TaskId: ID-014
Status: Pending
Priority: P1
Phase: "A Authentication"
Method: POST
Route: "/api/v1/me/sessions/revoke-others"
---

# ID-014 - Sign out other sessions

**Status: Pending** | **Priority: P1** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/me/sessions/revoke-others`

**User story:** US-014: As a user, I can keep my present session while signing out all other devices.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-013

## Request contract

**Path/query/header:** None

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 401 unauthenticated or revoked caller session.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Authenticated sid must identify an active caller session.
2. No target user ID accepted.

## Business logic

1. Bulk revoke all of caller AuthSessions except current session; revoke refresh tokens for affected sessions.
2. Keep current refresh family intact and audit how many sessions were invalidated (without secrets).

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokeOtherSessions command and transactional batch update method.
4. Reuse existing session revocation services.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Current device still refreshes while all other devices cannot.
2. Running twice is idempotent; no other user sessions affected.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
