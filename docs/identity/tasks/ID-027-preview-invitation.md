---
TaskId: ID-027
Status: Pending
Priority: P1
Phase: "C Invitations"
Method: POST
Route: "/api/v1/invitations/preview"
---

# ID-027 - Preview invitation

**Status: Pending** | **Priority: P1** | **Phase: C Invitations**

**Endpoint:** `POST /api/v1/invitations/preview`

**User story:** US-027: As an invite recipient, I can verify which company invited me before signing up or accepting.

**Actor:** Invitation token holder

**Authentication/authorization:** Public, token-throttled; small redacted response

**Dependencies:** ID-024

## Request contract

**Path/query/header:** None

**Body/DTO:** `PreviewInvitationRequest(string Token)`

**JSON example:**

```json
{
  "token": "<opaque-invitation-token>"
}
```

## Response contract

200 OK: InvitationPreviewResponse(string OrganizationDisplayName, string MaskedEmail, string MembershipType, DateTimeOffset ExpiresAt)

**Expected failures:** 400 malformed, 404 invalid/unavailable, 429 abuse throttled.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Token required 32..512 base64url chars; max request size; rate limit by IP/token prefix digest.
2. Only pending and unexpired invitations may be previewed.

## Business logic

1. Hash token, inspect invitation by digest and parent active tenant; return display name and masked target email.
2. Do not reveal role grants, tenant roster or unmasked email. Invalid/revoked/expired tokens share same negative result.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement PreviewInvitation query with safe minimal projection.
4. Keep token out of query string, analytics and logs.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Valid token shows company and masked recipient only.
2. Expired/invalid/revoked tokens return same generic negative result.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
