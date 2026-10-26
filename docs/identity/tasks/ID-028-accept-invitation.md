---
TaskId: ID-028
Status: Pending
Priority: P0
Phase: "C Invitations"
Method: POST
Route: "/api/v1/invitations/accept"
---

# ID-028 - Accept invitation

**Status: Pending** | **Priority: P0** | **Phase: C Invitations**

**Endpoint:** `POST /api/v1/invitations/accept`

**User story:** US-028: As a verified employee or client, I can join the organization with the roles originally approved.

**Actor:** Verified authenticated invitee

**Authentication/authorization:** Authenticated active, email-verified account whose email equals invitation target

**Dependencies:** ID-002, ID-024, ID-034; RLS bootstrap and guest policy

## Request contract

**Path/query/header:** None

**Body/DTO:** `AcceptInvitationRequest(string Token)`

**JSON example:**

```json
{
  "token": "<opaque-invitation-token>"
}
```

## Response contract

200 OK: AcceptInvitationResponse(Guid TenantId, Guid MemberId, string MembershipType, string MembershipStatus)

**Expected failures:** 401 not signed in, 403 email mismatch/inactive, 410 used/expired, 409 scope unavailable.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Token 32..512 chars; invitation pending, unexpired, not revoked; target tenant active.
2. Normalized verified caller PrimaryEmail must match InviteeEmail; role-grant scopes still active and assignable, guest restrictions enforced.

## Business logic

1. Lock invitation row; atomically find or create tenant membership (reactivate previously removed membership after clearing RemovedAt) and apply preselected role assignments.
2. Reactivation must NOT restore old roles/direct grants: those must have been revoked on removal. Mark invitation accepted and AcceptedByUserId/AcceptedAt, audit.
3. If target scope/role is now unavailable reject without consuming invitation; one time acceptance only.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement AcceptInvitation command with verified email guard and transaction-safe grant upserts.
4. Constrain all reads/writes by tenant and match role/scope kinds, handle unique membership race.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Valid recipient gets active membership and only approved grants; replay fails.
2. Different verified email, guest privilege escalation, suspended tenant or expired token never grant access.
3. Concurrent acceptance yields only one membership and one active assignment per requested role.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
