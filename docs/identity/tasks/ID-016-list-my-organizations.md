---
TaskId: ID-016
Status: Pending
Priority: P0
Phase: "B Organizations"
Method: GET
Route: "/api/v1/me/organizations"
---

# ID-016 - List my organizations

**Status: Pending** | **Priority: P0** | **Phase: B Organizations**

**Endpoint:** `GET /api/v1/me/organizations`

**User story:** US-016: As a member of several customers, I can choose which workspace to open.

**Actor:** Authenticated user

**Authentication/authorization:** Self only

**Dependencies:** ID-004, ID-015; privileged but self-constrained membership discovery under RLS

## Request contract

**Path/query/header:** None

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: IReadOnlyList<MyOrganizationResponse> where MyOrganizationResponse(Guid TenantId, string Slug, string DisplayName, string OrganizationStatus, Guid MemberId, string MembershipType, string MembershipStatus)

**Expected failures:** 401 unauthenticated.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. No caller userId input; cap list at 200 or paginate if organization membership count warrants.
2. Include membership status explicitly; default include only active membership and tenant.

## Business logic

1. Join shared.Organizations with dbo.OrganizationMembers by UserId derived from token.
2. Return accessible organizations only; no implicit role grants from another tenant; avoid RLS read-denial by using an audited self-membership discovery path.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListMyOrganizations query with an authorized cross-tenant self-membership read strategy.
4. Do not use untrusted app.tenant_id to probe arbitrary organization IDs.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. User with memberships in A/B sees A/B and not C.
2. Suspended tenant or member is excluded by default, without leaking unrelated names.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
