---
TaskId: ID-034
Status: Pending
Priority: P0
Phase: "D Access control"
Method: POST
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/roles"
---

# ID-034 - Assign role to member at scope

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/members/{memberId}/roles`

**User story:** US-034: As an authorized administrator, I can grant one predefined role for one project, team, performance or organization scope.

**Actor:** Organization admin/owner

**Authentication/authorization:** Tenant role delegation policy + appropriate manage permission at target scope

**Dependencies:** ID-030, ID-033 and access scope lifecycle from owning module

## Request contract

**Path/query/header:** None

**Body/DTO:** `AssignMemberRoleRequest(Guid ScopeId, Guid RoleId, DateTimeOffset? ExpiresAt, string? Reason); route tenantId and memberId Guid`

**JSON example:**

```json
{
  "scopeId": "00000000-0000-7000-8000-000000000001",
  "roleId": "00000000-0000-7000-8000-000000000002",
  "expiresAt": "2027-01-01T00:00:00Z",
  "reason": "Temporary project ownership"
}
```

## Response contract

201 Created: MemberRoleAssignmentResponse(Guid Id, Guid ScopeId, string ScopeKind, Guid RoleId, string RoleCode, DateTimeOffset AssignedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 invalid fields, 403 escalation, 404 foreign IDs, 409 duplicate/expired prerequisite.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. ScopeId and RoleId valid UUID; same scope kind; scope active and tenant-bound; role active, assignable.
2. Member active and tenant matches; ExpiresAt > now and <= now+365 days if provided; Reason <=300.
3. No self-elevation, organization_owner/platform roles restricted to owner/privileged bootstrap; guests only permitted safe role types.

## Business logic

1. Check delegator is allowed to grant this exact role in that exact scope, not merely that they have members.manage.
2. If an expired but unrevoked duplicate exists, revoke it before insert; atomically create MemberRoleAssignment and tenant audit.
3. Prevent duplicate active assignments with unique index and transaction; invalidate permission cache.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement AssignMemberRole command, validator and role/scope delegation policy in application layer.
4. Use PostgreSQL constraint handling for composite tenant/scope FK and concurrent uniqueness; use transactional persistence.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Role applies at requested scope only; no implicit scope inheritance.
2. Guest cannot gain confidential performance or owner privileges; unauthorized grants denied.
3. Concurrent duplicate role assignment does not create two active rows.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
