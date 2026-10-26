---
TaskId: ID-020
Status: Pending
Priority: P1
Phase: "B Organizations"
Method: GET
Route: "/api/v1/organizations/{tenantId}/members/{memberId}"
---

# ID-020 - Get organization member

**Status: Pending** | **Priority: P1** | **Phase: B Organizations**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/members/{memberId}`

**User story:** US-020: As an authorized manager, I can inspect one member profile in my organization.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.read

**Dependencies:** ID-019

## Request contract

**Path/query/header:** tenantId: Guid, memberId: Guid (UUIDv7)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: MemberResponse(Guid Id, Guid UserId, string DisplayName, string PrimaryEmail, string MembershipType, string Status, DateTimeOffset JoinedAt)

**Expected failures:** 400 malformed UUID, 401/403, 404 target hidden.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Both UUIDs valid; member belongs to tenant.
2. No sensitive user credentials or private review fields in projection.

## Business logic

1. Fetch member by composite tenant/member key; join shared user profile for safe details.
2. Hide missing and cross-tenant member as 404.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetMember query with composite tenant filter.
4. Use same MemberResponse as list route.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Correct member returned with safe columns.
2. Foreign tenant/member combination yields 404, never cross-tenant data.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
