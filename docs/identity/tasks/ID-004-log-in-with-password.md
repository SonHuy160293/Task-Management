---
TaskId: ID-004
Status: Pending
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/login"
---

# ID-004 - Log in with password

**Status: Pending** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/login`

**User story:** US-004: As a verified user, I can sign in securely and start a device session.

**Actor:** Anonymous

**Authentication/authorization:** Public; authentication throttling

**Dependencies:** ID-002, auth JWT and cookie setup; real IUnitOfWork

## Request contract

**Path/query/header:** None

**Body/DTO:** `LoginRequest(string Email, string Password)`

**JSON example:**

```json
{
  "email": "alice@example.com",
  "password": "a long unique passphrase 2026"
}
```

## Response contract

200 OK: LoginResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAt, CurrentUserResponse User); refresh token ONLY in Secure HttpOnly cookie, never JSON

**Expected failures:** 400 malformed, 401 invalid credentials, 403 inactive/unverified, 429 throttled.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Email valid length 3..320 and password nonblank bounded <=1024; reject malformed input without logging password.
2. Rate limit by IP/account, honor PasswordCredential.LockedUntil and FailedAttempts.

## Business logic

1. Lookup normalized email, verify Argon2id with timing protections; generic 401 for wrong credentials or unknown email.
2. Only active, email-verified user may sign in. Atomically create AuthSession and hashed opaque RefreshToken; update LastLoginAt and failure counters.
3. Issue short-lived JWT with sub/user-id, sid/session-id, exp, iss, aud; no stale tenant/permission claims; set refresh cookie SameSite=Strict for same-site SPA and CSRF/origin defense.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement Login command and password/session/token services with injectable clock and cryptographic randomness.
4. Add cookie writer in controller, preserve CQRS flow and unit of work transaction for session creation.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Correct credentials yield valid access token plus refresh cookie, with only user-safe profile fields.
2. Unknown/wrong password indistinguishable; lockout and rate limits work; pending/suspended denied.
3. Stored refresh secret is 32-byte digest, never raw token; cookie Secure/HttpOnly/SameSite/Path are set.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
