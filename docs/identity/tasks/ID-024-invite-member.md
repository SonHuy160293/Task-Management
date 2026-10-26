---
TaskId: ID-024
Status: Pending
Priority: P0
Phase: "C Invitations"
Method: POST
Route: "/api/v1/organizations/{tenantId}/invitations"
---

# ID-024 - Invite member

**Status: Pending** | **Priority: P0** | **Phase: C Invitations**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/invitations`

**User story:** US-024: As an owner/admin, I can invite an employee, contractor or restricted external client with preapproved roles.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.members.invite; grant delegation on EVERY proposed role/scope

**Dependencies:** ID-015, ID-019, ID-030 and role catalog seed; mailer; scope registration by owner module

## Request contract

**Path/query/header:** None

**Body/DTO:** `CreateInvitationRequest(string InviteeEmail, string MembershipType, IReadOnlyList<InvitationRoleInput> RoleGrants) where InvitationRoleInput(Guid ScopeId, Guid RoleId)`

**JSON example:**

```json
{
  "inviteeEmail": "client@partner.com",
  "membershipType": "guest",
  "roleGrants": [
    {
      "scopeId": "00000000-0000-7000-8000-000000000001",
      "roleId": "00000000-0000-7000-8000-000000000002"
    }
  ]
}
```

## Response contract

201 Created: InvitationResponse(Guid Id, string InviteeEmail, string MembershipType, string Status, DateTimeOffset ExpiresAt, IReadOnlyList<RoleGrantSummary> RoleGrants)

**Expected failures:** 400 invalid fields, 403 delegation/guest restriction, 409 pending invite/member exists.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Email format <=320, membershipType in employee|contractor|guest, RoleGrants 1..20 unique scope-role pairs, UUIDs valid.
2. Target scopes belong to tenant and active; role must be active, assignable and match scope kind.
3. Require one organization-scope baseline membership role; guests may only get allowlisted non-sensitive project/team/viewer scopes, no performance or org admin roles.

## Business logic

1. Validate inviter can delegate each requested role/permission without escalating own privilege; reject already-active membership and pending unexpired invite.
2. Expire stale pending invite before re-creation to satisfy partial unique index; generate strong opaque token, persist SHA-256 hash and role-grant rows atomically, queue delivery to email.
3. Do not include token in API response, log audit.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement InviteMember command, validator, invitation/grant repositories and mail integration.
4. Use transaction and unique index to protect duplicate invite race, check scope kind/composite FK.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Guest invite limited to authorized project-viewer role; invalid escalation rejected.
2. Only one pending invite per tenant+email; token hash (32 bytes) stored and raw token never returned.
3. Invite creates invitation + role grants atomically; email delivery queued.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
