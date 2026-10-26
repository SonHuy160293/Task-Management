---
TaskId: ID-029
Status: Pending
Priority: P1
Phase: "C Invitations"
Method: POST
Route: "/api/v1/organizations/{tenantId}/invitations/{invitationId}/resend"
---

# ID-029 - Resend invitation

**Status: Pending** | **Priority: P1** | **Phase: C Invitations**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/invitations/{invitationId}/resend`

**User story:** US-029: As an admin, I can resend a lost invitation without creating a duplicate pending invitation.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.invite and delegation recheck

**Dependencies:** ID-024, ID-026

## Request contract

**Path/query/header:** tenantId: Guid, invitationId: Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

202 Accepted: GenericMessageResponse(string Message)

**Expected failures:** 403 unauthorized, 404 missing, 409 accepted/revoked or invalid grants, 429 cooldown.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Only own tenant invitation; must be pending OR explicitly expired state if reactivated.
2. Apply email cooldown and rate limit; current inviter still entitled to grant all listed roles.

## Business logic

1. Rotate TokenHash in place and extend ExpiresAt; invalidate old token immediately; persist new secret digest only.
2. Revalidate all planned role grants, scope status, guest policy; queue new email and audit; rejected resend changes nothing.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ResendInvitation command with token rotation and safe mail acceptance.
4. Account for partial unique pending invitation constraint; avoid inserting second pending row for same email.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Old token fails and new emailed token succeeds; no duplicate invite created.
2. Unauthorized or role-escalating resend is denied, with no email sent.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
