---
TaskId: ID-026
Status: Pending
Priority: P0
Phase: "C Invitations"
Method: DELETE
Route: "/api/v1/organizations/{tenantId}/invitations/{invitationId}"
---

# ID-026 - Revoke invitation

**Status: Pending** | **Priority: P0** | **Phase: C Invitations**

**Endpoint:** `DELETE /api/v1/organizations/{tenantId}/invitations/{invitationId}`

**User story:** US-026: As an organization admin, I can cancel an invitation before it is redeemed.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.invite and original grant-delegation authority if required by policy

**Dependencies:** ID-024

## Request contract

**Path/query/header:** tenantId: Guid, invitationId: Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 403 unauthorized, 404 foreign, 409 already accepted.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Invitation belongs to tenant; only pending may be revoked; repeated revoke idempotent.
2. Cannot revoke accepted invitation to remove a member; use member removal instead.

## Business logic

1. Change invitation Status=revoked, RevokedAt=now transactionally; after this accepting the token fails.
2. Do not modify existing accepted membership; audit revocation.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokeInvitation command and conditional status transition.
4. Use composite tenant/invitation ID to prevent IDOR.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Revoked invitation cannot be accepted.
2. Foreign invite ID returns 404; accepted invite cannot be used as offboarding.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
