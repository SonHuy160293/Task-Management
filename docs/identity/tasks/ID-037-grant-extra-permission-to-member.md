---
TaskId: ID-037
Status: Pending
Priority: P0
Phase: "D Access control"
Method: POST
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/permissions"
---

# ID-037 - Grant extra permission to member

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `POST /api/v1/organizations/{tenantId}/members/{memberId}/permissions`

**User story:** US-037: As an authorized manager, I can temporarily grant a developer a specific extra capability outside their normal role.

**Actor:** Organization owner/admin or scope manager

**Authentication/authorization:** permission delegation policy for this exact permission and scope

**Dependencies:** ID-032, ID-036, scoped authorization policy and correct catalog IsSensitive

## Request contract

**Path/query/header:** None

**Body/DTO:** `GrantMemberPermissionRequest(Guid ScopeId, Guid PermissionId, string Justification, DateTimeOffset? ExpiresAt); route tenantId/memberId Guid`

**JSON example:**

```json
{
  "scopeId": "00000000-0000-7000-8000-000000000001",
  "permissionId": "00000000-0000-7000-8000-000000000003",
  "justification": "Temporary release support approved by lead",
  "expiresAt": "2026-11-15T00:00:00Z"
}
```

## Response contract

201 Created: MemberPermissionGrantResponse(Guid Id, Guid ScopeId, string ScopeKind, Guid PermissionId, string PermissionCode, string Justification, DateTimeOffset GrantedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 justification/expiration, 403 escalation, 404 foreign, 409 duplicate.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Justification trimmed 8..500; ScopeId/PermissionId UUID valid; scope active and tenant-bound; scope-kind match.
2. Target member active and permitted for guest restriction; permission active; never allow platform scope for dbo grants.
3. ExpiresAt must be future if present; REQUIRED and <=90 days for IsSensitive; recommended max 365 days otherwise.

## Business logic

1. Check granting actor is authorized to delegate this specific permission at this scope; cannot grant what actor cannot delegate, even if actor can manage membership.
2. Revoke expired/unrevoked duplicate first or update transactionally; insert ALLOW-only grant and audit, invalidate permission cache.
3. Enforce performance confidentiality in owning module even if a grant is present.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GrantMemberPermission command, validator and constrained delegation service.
4. Use tenant+member+scope checks and DB partial uniqueness within transaction.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Extra permission works only at its exact scope and expires/revokes promptly.
2. Guest cannot receive sensitive performance or organization admin permissions.
3. Duplicate concurrent grant does not create two active rows.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
