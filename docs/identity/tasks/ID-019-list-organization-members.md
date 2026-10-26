---
TaskId: ID-019
Status: Pending
Priority: P0
Phase: "B Organizations"
Method: GET
Route: "/api/v1/organizations/{tenantId}/members"
---

# ID-019 - List organization members

**Status: Pending** | **Priority: P0** | **Phase: B Organizations**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/members`

**User story:** US-019: As an organization administrator, I can see all users assigned to my workspace.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.read at organization scope

**Dependencies:** ID-015, RBAC permissions seed

## Request contract

**Path/query/header:** status?: active|suspended|removed; membershipType?: employee|contractor|guest; page?: int=1; pageSize?: int=20

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: PagedResponse<MemberResponse> with MemberResponse(Guid Id, Guid UserId, string DisplayName, string PrimaryEmail, string MembershipType, string Status, DateTimeOffset JoinedAt)

**Expected failures:** 400 filters, 401/403, 404 foreign org.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. page 1..100000, pageSize 1..100; status/type enum values only.
2. Tenant must be active and caller must have read-members permission.

## Business logic

1. Query tenant-bound dbo.OrganizationMembers with shared.Users safe fields, filter and deterministic sort JoinedAt DESC + Id.
2. No query may leak cross-tenant users; no password credential data.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListMembers query with no-tracking projection and pagination.
4. Use RLS tenant read context for both tenant and user join.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Filters and pagination produce stable bounded results.
2. Guest/outsider cannot enumerate members; tenant B cannot see A.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
