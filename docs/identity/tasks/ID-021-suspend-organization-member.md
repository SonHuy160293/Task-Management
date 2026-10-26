---
TaskId: ID-021
Status: Pending
Priority: P0
Phase: "B Organizations"
Method: POST
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/suspend"
---

# ID-021 - Suspend organization member

**Status: Pending** | **Priority: P0** | **Phase: B Organizations**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/members/{memberId}/suspend`

**User story:** US-021: As an organization owner/admin, I can immediately block an employee or contractor from this workspace.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.members.manage; last-owner and self-suspension rules

**Dependencies:** ID-019, ID-034 for owner detection, effective-access cache strategy

## Request contract

**Path/query/header:** None

**Body/DTO:** `SuspendMemberRequest(string Reason); route: tenantId: Guid, memberId: Guid`

**JSON example:**

```json
{
  "reason": "Employment contract ended"
}
```

## Response contract

204 No Content

**Expected failures:** 400 reason, 403 protected target, 404 foreign/missing, 409 already suspended or last owner.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reason trimmed 8..300; member belongs to active tenant, current status=active.
2. Cannot suspend last active organization_owner; no self-suspension in V1.
3. Actor must be allowed to manage target; organization admins cannot suspend owners.

## Business logic

1. Atomically change Membership.Status=suspended; preserve roles for later reactivation but authorization must reject suspended membership on every request.
2. Lock membership/organization owner set to prevent concurrent last-owner removal, write security audit with reason.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement SuspendMember command with target-scope and role-hierarchy checks.
4. Reuse atomic last-owner guard; invalidate effective-grant caches synchronously.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Suspended member immediately cannot access tenant data, even using existing JWT.
2. Two concurrent owner suspensions cannot remove last active owner.
3. Unauthorized admin cannot suspend owner or another tenant member.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
