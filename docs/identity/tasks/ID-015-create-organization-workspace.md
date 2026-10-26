---
TaskId: ID-015
Status: Pending
Priority: P0
Phase: "B Organizations"
Method: POST
Route: "/api/v1/organizations"
---

# ID-015 - Create organization workspace

**Status: Pending** | **Priority: P0** | **Phase: B Organizations**

**Endpoint:** `POST /api/v1/organizations`

**User story:** US-015: As a verified account holder, I can create a company workspace and become its first owner.

**Actor:** Verified authenticated user

**Authentication/authorization:** Authenticated active user; no existing tenant required

**Dependencies:** ID-002, seed organization_owner; tenant DB/RLS bootstrap

## Request contract

**Path/query/header:** None

**Body/DTO:** `CreateOrganizationRequest(string Slug, string DisplayName)`

**JSON example:**

```json
{
  "slug": "novatech",
  "displayName": "NovaTech Software"
}
```

## Response contract

201 Created: OrganizationResponse(Guid Id, string Slug, string DisplayName, string Status, DateTimeOffset CreatedAt); Location => GET /organizations/{tenantId}

**Expected failures:** 400 invalid slug, 401 unauthenticated, 403 unverified, 409 slug taken.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Slug lowercase 3..80 and regex ^[a-z0-9][a-z0-9-]{2,79}$; globally case-insensitive unique.
2. DisplayName trimmed 1..200; creator user active and email verified.
3. Require seeded organization_owner and organization_member roles with scope_kind organization.

## Business logic

1. In one transaction create shared.Organization, dbo.OrganizationMember for creator, dbo.AccessScope organization resource_id=tenantId, dbo.MemberRoleAssignment organization_owner; optional member role only if policy requires it.
2. Use UUIDv7 for every entity, validate owner assignment and duplicate-slug race via database unique index.
3. Write tenant audit event after creating tenant with valid RLS bootstrap context.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement CreateOrganization command and atomic bootstrap repository/service routine, call sender from controller.
4. Map conflict from unique slug; use Created(uri, dto) with Location instead of CreatedAtAction until GET route ID-017 exists.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Successful response refers to persisted org; creator is active sole owner and has organization scope.
2. Rollback leaves no partial org, scope or membership.
3. Concurrent identical slug creates at most one org.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
