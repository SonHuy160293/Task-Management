---
TaskId: ID-022
Status: Pending
Priority: P1
Phase: "B Organizations"
Method: POST
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/reactivate"
---

# ID-022 - Reactivate suspended member

**Status: Pending** | **Priority: P1** | **Phase: B Organizations**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/members/{memberId}/reactivate`

**User story:** US-022: As an owner/admin, I can restore access to a temporarily suspended member.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.members.manage; delegated target limits

**Dependencies:** ID-021

## Request contract

**Path/query/header:** None

**Body/DTO:** `ReactivateMemberRequest(string Reason); route: tenantId: Guid, memberId: Guid`

**JSON example:**

```json
{
  "reason": "Returned from approved leave"
}
```

## Response contract

204 No Content

**Expected failures:** 400 reason, 403 actor/global state, 404 missing, 409 invalid transition.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reason 8..300; only status=suspended may transition to active.
2. Global user and organization must be active; removed status requires invitation rejoin instead.

## Business logic

1. Change membership to active with UpdatedAt, retain unrevoked/unexpired grants as before suspension.
2. Write audit and invalidate authorization cache; no recreation of canceled grants.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ReactivateMember command and transition checks.
4. Reuse tenant auth resolver and audit service.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Suspended member becomes active and regains only currently valid grants.
2. Globally suspended user stays denied; removed member cannot be reactivated this way.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
