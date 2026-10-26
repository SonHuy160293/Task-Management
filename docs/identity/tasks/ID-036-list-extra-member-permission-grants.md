---
TaskId: ID-036
Status: Pending
Priority: P0
Phase: "D Access control"
Method: GET
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/permissions"
---

# ID-036 - List extra member permission grants

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/members/{memberId}/permissions`

**User story:** US-036: As an authorized admin, I can review exceptional permissions granted directly to a user.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.permissions.manage or access.grants.read

**Dependencies:** ID-019, ID-032

## Request contract

**Path/query/header:** tenantId and memberId Guid; query: scopeId?: Guid, includeRevoked?: bool=false

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<MemberPermissionGrantResponse> where MemberPermissionGrantResponse(Guid Id, Guid ScopeId, string ScopeKind, Guid PermissionId, string PermissionCode, string Justification, DateTimeOffset GrantedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 filters, 403 unauthorized, 404 foreign member.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Target member and optional scope must belong to tenant.
2. includeRevoked requires audit-level access; hide privileged descriptions from guests.

## Business logic

1. Filter dbo.MemberPermissionGrants on tenant/member, join active catalog and scope, order GrantedAt DESC.
2. Treat expired grants as not effective even if RevokedAt null.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListMemberPermissionGrants query/DTO with tenant-aware join.
4. Do not confuse extra grants with role-derived permissions.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Cross-tenant and other member data denied.
2. Expiry/revocation timestamps shown correctly and no secrets returned.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
