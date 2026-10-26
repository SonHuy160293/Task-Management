---
TaskId: ID-043
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: POST
Route: "/api/v1/platform/organizations/{tenantId}/reactivate"
---

# ID-043 - Reactivate tenant organization

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `POST /api/v1/platform/organizations/{tenantId}/reactivate`

**User story:** US-043: As platform staff, I can lift a tenant suspension after investigation.

**Actor:** Platform admin

**Authentication/authorization:** platform.tenants.suspend or explicit platform.tenants.reactivate

**Dependencies:** ID-042

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangeTenantStatusRequest(string Reason); route tenantId Guid`

**JSON example:**

```json
{
  "reason": "Security investigation completed successfully"
}
```

## Response contract

204 No Content

**Expected failures:** 400 reason, 403 actor, 404 tenant, 409 invalid state.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reason 8..500; only currently suspended tenant may reactivate; closed remains closed.
2. Only authorized platform actor.

## Business logic

1. Set Status=active and SuspendedAt=null under transaction; retain former suspension in append-only platform audit.
2. Invalidate tenant cache; keep suspended individual memberships suspended.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ReactivateTenant command and state-transition checks.
4. Reuse platform audit writer and explicit permission gate.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Organization resumes with existing valid memberships only.
2. Closed org not reopened and already-active transition does not mutate status.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
