---
TaskId: ID-033
Status: Pending
Priority: P0
Phase: "D Access control"
Method: GET
Route: "/api/v1/organizations/{tenantId}/members/{memberId}/roles"
---

# ID-033 - List member role assignments

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/members/{memberId}/roles`

**User story:** US-033: As an organization admin, I can inspect current role assignments and scope boundaries.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.manage or access.assignments.read at org scope

**Dependencies:** ID-019, ID-030

## Request contract

**Path/query/header:** tenantId: Guid, memberId: Guid; query: scopeId?: Guid, includeRevoked?: bool=false

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<MemberRoleAssignmentResponse> where MemberRoleAssignmentResponse(Guid Id, Guid ScopeId, string ScopeKind, Guid RoleId, string RoleCode, DateTimeOffset AssignedAt, DateTimeOffset? ExpiresAt, DateTimeOffset? RevokedAt)

**Expected failures:** 400 UUID/query, 403 caller, 404 foreign target.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. memberId must belong to tenant; scopeId if supplied also must belong to tenant.
2. includeRevoked allowed only to privileged member-access auditor.

## Business logic

1. Join dbo.MemberRoleAssignments to shared.Roles and dbo.AccessScopes by tenant-bound keys; default active/unrevoked results.
2. Do not conflate expired (not active) assignment with revoked; include precise timestamps.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetMemberRoleAssignments query, filtered projection and authorization.
4. Use consistent tenant-aware query strategy with RLS.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Only own-tenant assignments displayed; revoked hidden by default.
2. Cross-tenant member or scope returns 404.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
