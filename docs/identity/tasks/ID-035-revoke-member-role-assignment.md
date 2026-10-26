---
TaskId: ID-035
Status: Pending
Priority: P0
Phase: "D Access control"
Method: DELETE
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/roles/{assignmentId}"
---

# ID-035 - Revoke member role assignment

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `DELETE /api/v1/organizations/{tenantId}/members/{memberId}/roles/{assignmentId}`

**User story:** US-035: As an administrator, I can remove delegated access without deleting its historical record.

**Actor:** Organization admin/owner

**Authentication/authorization:** Role revocation delegation + last-owner protection

**Dependencies:** ID-034

## Request contract

**Path/query/header:** tenantId, memberId, assignmentId: Guid

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

204 No Content

**Expected failures:** 403 forbidden grant/owner, 404 foreign, 409 last owner.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Composite target IDs match tenant and member; revocation idempotent.
2. Cannot revoke the organization's last active owner role; ordinary admins cannot revoke owner role.

## Business logic

1. Lock tenant owner set if owner role affected; set RevokedAt and keep row for audit/history.
2. Invalidate cached effective permissions and audit actor/target/scope without leaking private data.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement RevokeMemberRole command with target/delegation/last-owner checks.
4. Use conditional update and idempotent already-revoked result.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Permission stops being effective after revocation.
2. Last active owner cannot be removed even with simultaneous revoke requests.
3. Foreign tenant assignment returns 404.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
