---
TaskId: ID-012
Status: Pending
Priority: P1
Phase: "A Authentication"
Method: GET
Route: "/api/v1/me/sessions"
---

# ID-012 - List my sessions

**Status: Pending** | **Priority: P1** | **Phase: A Authentication**

**Endpoint:** `GET /api/v1/me/sessions`

**User story:** US-012: As a user, I can review devices where my account is signed in.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-004

## Request contract

**Path/query/header:** None

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<SessionResponse> where SessionResponse(Guid Id, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? LastSeenAt, string? ClientIpMasked, string? UserAgent, bool IsCurrent, bool IsRevoked)

**Expected failures:** 401 unauthenticated.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. No user ID query/path; results scoped to current user.
2. Return recent active and revoked sessions within a bounded retention window, max 100.

## Business logic

1. Select sessions by caller UserId; compute IsCurrent from validated sid and omit raw tokens.
2. Redact IP and cap/clean UserAgent from response.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListMySessions query with safe DTO projection.
4. Apply deterministic order CreatedAt descending.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Other user sessions never appear.
2. Current session correctly marked; never return refresh token hashes.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
