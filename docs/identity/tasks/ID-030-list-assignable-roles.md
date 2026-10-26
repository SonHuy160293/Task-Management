---
TaskId: ID-030
Status: Pending
Priority: P0
Phase: "D Access control"
Method: GET
Route: "/api/v1/access/roles"
---

# ID-030 - List assignable roles

**Status: Pending** | **Priority: P0** | **Phase: D Access control**

**Endpoint:** `GET /api/v1/access/roles`

**User story:** US-030: As an authorized administrator, I can discover system-approved roles before granting access.

**Actor:** Organization admin or platform admin

**Authentication/authorization:** Authenticated; org admin for tenant-level view; platform admin for platform roles

**Dependencies:** Role catalog seed, organization membership access resolver

## Request contract

**Path/query/header:** None

**Body/DTO:** `No request body. Query: scopeKind: platform|organization|project|team|performance (required); tenantId?: Guid for tenant-context policy`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<RoleCatalogResponse> where RoleCatalogResponse(Guid Id, string Code, string DisplayName, string ScopeKind, string? Description, bool IsAssignable)

**Expected failures:** 400 scopeKind/tenantId, 401, 403.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. scopeKind required from controlled enum; tenantId required for organization/project/team/performance scopes.
2. Do not return platform roles to tenant-only admins; active and assignable roles only.

## Business logic

1. Read shared.Roles filtered by scope kind and assignment policy; never allow tenant-managed arbitrary role creation.
2. Resolve requester tenant membership for tenant view and enforce manage/invite permission.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListRoles query, safe catalog projection and permission gate.
4. Keep roles as global seed definitions; no mutation endpoints.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Tenant admin sees eligible tenant roles only, platform-only roles hidden.
2. Inactive/non-assignable roles excluded; no unauthenticated access.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
