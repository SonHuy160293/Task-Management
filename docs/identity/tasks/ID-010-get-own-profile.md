---
TaskId: ID-010
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: GET
Route: "/api/v1/me"
---

# ID-010 - Get own profile

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `GET /api/v1/me`

**User story:** US-010: As an authenticated user, I can see my own safe account information.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-004

## Request contract

**Path/query/header:** None

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: CurrentUserResponse(Guid Id, string PrimaryEmail, string DisplayName, string? AvatarUrl, string Locale, string TimeZone, string Status, DateTimeOffset? EmailVerifiedAt)

**Expected failures:** 401 unauthenticated, 403 inactive user.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. No client-supplied user ID; caller identity derived from validated token.
2. User and session must remain active.

## Business logic

1. Query shared.Users by caller sub, project only non-secret columns; never expose password hash, reset token, refresh hash or platform-role privileged data.
2. Use no-tracking repository query and standard result mapping.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetCurrentUser query, response DTO, handler and controller route.
4. Re-use existing JWT current-user abstraction rather than parsing tokens in controller.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Caller gets exactly their own profile; anonymous =>401.
2. Response contains no credential, session secret or another user data.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
