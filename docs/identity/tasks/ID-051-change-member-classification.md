---
TaskId: ID-051
Status: Pending
Priority: P1
Phase: "B Organizations"
Method: PUT
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/membership-type"
---

# ID-051 - Change member classification

**Status: Pending** | **Priority: P1** | **Phase: B Organizations**

**Endpoint:** `PUT /api/v1/organizations/{tenantId}/members/{memberId}/membership-type`

**User story:** US-051: As an organization owner/admin, I can convert an employee to contractor/guest status while immediately restricting inappropriate access.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.members.manage, restricted role/grant delegation policy

**Dependencies:** ID-019, ID-034, ID-035, ID-037, ID-038; catalog baseline roles

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangeMemberTypeRequest(string MembershipType, string Reason); route tenantId and memberId Guid`

**JSON example:**

```json
{
  "membershipType": "guest",
  "reason": "Project completed; client-only collaboration now"
}
```

## Response contract

200 OK: MemberResponse(Guid Id, Guid UserId, string DisplayName, string PrimaryEmail, string MembershipType, string Status, DateTimeOffset JoinedAt)

**Expected failures:** 400 type/reason, 403 actor/owner, 404 foreign, 409 unsafe transition.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. MembershipType exactly employee|contractor|guest; Reason trimmed 8..300; active member in active tenant.
2. Cannot make active owner a guest or change own classification in V1; only owner may change privileged targets.
3. Check Role/Permission catalog mappings for safe guest roles and direct grants.

## Business logic

1. Lock member and access assignments; if downgrading to guest, atomically revoke roles/direct grants incompatible with guests and assign safe organization_guest baseline if needed.
2. If guest upgrades to employee/contractor, revoke obsolete guest baseline if policy requires and assign organization_member baseline; do NOT auto-grant admin/project privileges.
3. Change MembershipType, UpdatedAt, security audit with prior/new classification and invalidate access cache.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ChangeMemberType command and strict transition/last-owner/guest-safety checks.
4. Reuse role assignment and direct grant repositories with transactional revocation + safe baseline reassignment.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Employee->guest immediately loses org-wide, audit and performance permissions despite cached JWT.
2. Guest->employee does not automatically become admin/manager; historical events preserved.
3. Forbidden owner downgrade and foreign tenant member both denied.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
