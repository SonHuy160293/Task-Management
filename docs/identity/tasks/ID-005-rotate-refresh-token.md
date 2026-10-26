---
TaskId: ID-005
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/refresh"
---

# ID-005 - Rotate refresh token

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/refresh`

**User story:** US-005: As a signed-in user, I can renew an access token without reentering credentials.

**Actor:** Refresh-cookie holder

**Authentication/authorization:** Cookie-bound; CSRF/origin protection; no access token required

**Dependencies:** ID-004

## Request contract

**Path/query/header:** Refresh token: HttpOnly Cookie (no JSON payload)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: RefreshResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt); replaces refresh cookie with new opaque secret

**Expected failures:** 401 invalid/expired/replayed, 403 disabled user, 429 abuse limits.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reject requests without cookie or with untrusted Origin/CSRF proof; bound cookie size.
2. Token must hash to exactly one active, unconsumed, unexpired row; valid session and active global user.

## Business logic

1. In a single locked transaction consume existing token, mint hashed successor and link ReplacedByTokenId; keep session expiry absolute.
2. If consumed token is reused, revoke the session/token family and require sign-in; do not permit double rotation under racing requests.
3. Issue new short-lived JWT tied to session; clear cookie on failure.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement Refresh command and atomic compare-and-set/row lock for token rotation.
4. Reuse access token creation and cookie policy; audit replay detection.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Refresh succeeds once; old token reuse is rejected and revokes session.
2. Two concurrent rotations cannot both succeed; expired/revoked session yields 401.
3. Raw refresh token never appears in body/log/database.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
