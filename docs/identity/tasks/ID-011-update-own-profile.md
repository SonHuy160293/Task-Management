---
TaskId: ID-011
Status: Pending
Priority: P1
Phase: "A Authentication"
Method: PUT
Route: "/api/v1/me"
---

# ID-011 - Update own profile

**Status: Pending** | **Priority: P1** | **Phase: A Authentication**

**Endpoint:** `PUT /api/v1/me`

**User story:** US-011: As a user, I can update display and localization preferences.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-010

## Request contract

**Path/query/header:** None

**Body/DTO:** `UpdateMyProfileRequest(string DisplayName, string? AvatarUrl, string Locale, string TimeZone)`

**JSON example:**

```json
{
  "displayName": "Alice Nguyen",
  "avatarUrl": "https://cdn.example.com/a.png",
  "locale": "vi-VN",
  "timeZone": "Asia/Ho_Chi_Minh"
}
```

## Response contract

200 OK: CurrentUserResponse(Guid Id, string PrimaryEmail, string DisplayName, string? AvatarUrl, string Locale, string TimeZone, string Status, DateTimeOffset? EmailVerifiedAt)

**Expected failures:** 400 invalid fields, 401 unauthenticated.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. DisplayName trimmed 1..160, avatar HTTPS URL max 2048 or null, Locale 1..16 valid tag, TimeZone 1..64 IANA identifier.
2. Request cannot change PrimaryEmail, Status, EmailVerifiedAt or ID.

## Business logic

1. Load caller User; update only four permitted profile fields, UpdatedAt via existing audit approach.
2. Return persisted projection and audit non-sensitive account profile change.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement UpdateMyProfile command and validator.
4. Use existing entity methods or established update pattern; no accidental unrelated field mutation.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Valid preferences saved and reflected on GET /me.
2. Attempts to mutate protected fields via extra JSON are rejected by JSON contract policy or ignored consistently.
3. Other users are unaffected.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
