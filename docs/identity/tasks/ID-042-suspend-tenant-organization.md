---
TaskId: ID-042
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: POST
Route: "/api/v1/platform/organizations/{tenantId}/suspend"
---

# ID-042 - Suspend tenant organization

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `POST /api/v1/platform/organizations/{tenantId}/suspend`

**User story:** US-042: As a platform administrator, I can block an abusive or compromised tenant.

**Actor:** Platform admin

**Authentication/authorization:** platform.tenants.suspend

**Dependencies:** ID-041, tenant status access enforcement

## Request contract

**Path/query/header:** None

**Body/DTO:** `ChangeTenantStatusRequest(string Reason); route tenantId Guid`

**JSON example:**

```json
{
  "reason": "Confirmed contractual compliance hold"
}
```

## Response contract

204 No Content

**Expected failures:** 400 reason, 403 unauthorized, 404 missing, 409 already suspended/closed.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Reason trimmed 8..500; organization exists with Status=active.
2. Caller must be active platform admin; prefer step-up auth for high-risk operation when available.

## Business logic

1. Lock organization row, set Status=suspended, SuspendedAt=now, update timestamp and platform audit.
2. Tenant access gate must immediately reject all membership and resource access, even for valid unexpired JWT and owner role.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement SuspendTenant command with platform authorization and audit.
4. Invalidate tenant-active cache without modifying members or role rows.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Suspended tenant members lose access to all tenant data immediately.
2. Other organizations for same global user remain usable; audit includes reason and actor.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
