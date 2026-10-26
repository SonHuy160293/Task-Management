---
TaskId: ID-041
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: GET
Route: "/api/v1/platform/organizations"
---

# ID-041 - List organizations for platform support

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `GET /api/v1/platform/organizations`

**User story:** US-041: As a platform operator, I can find tenant workspaces to perform governance tasks.

**Actor:** Platform admin

**Authentication/authorization:** Active global user with platform.tenants.read

**Dependencies:** ID-004, platform role seeding and bootstrap

## Request contract

**Path/query/header:** status?: active|suspended|closed; search?: string; page?: int=1; pageSize?: int=20

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: PagedResponse<PlatformOrganizationResponse> where PlatformOrganizationResponse(Guid Id, string Slug, string DisplayName, string Status, DateTimeOffset CreatedAt, DateTimeOffset? SuspendedAt)

**Expected failures:** 400 filters, 401, 403.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. pageSize 1..100, search <=100 trimmed; allowed status values.
2. Platform roles from shared.PlatformRoleAssignments, not any tenant owner role.

## Business logic

1. Query shared.Organizations only, apply filters and pagination; no tenant project/task data.
2. Audit sensitive administrative query if policy requires; ensure platform permission lookup uses active role and valid user.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListPlatformOrganizations query and platform authorization policy.
4. Do not treat platform admin as blanket tenant membership.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Platform operator sees registries only; tenant admin cannot use endpoint.
2. Search/pagination stable, no cross-tenant business data.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
