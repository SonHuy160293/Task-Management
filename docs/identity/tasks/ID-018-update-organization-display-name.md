---
TaskId: ID-018
Status: Pending
Priority: P1
Phase: "B Organizations"
Method: PUT
Route: "/api/v1/organizations/{tenantId}"
---

# ID-018 - Update organization display name

**Status: Pending** | **Priority: P1** | **Phase: B Organizations**

**Endpoint:** `PUT /api/v1/organizations/{tenantId}`

**User story:** US-018: As an organization owner, I can rename our workspace without changing its permanent slug.

**Actor:** Organization owner/admin

**Authentication/authorization:** organization.settings.manage at organization scope

**Dependencies:** ID-017, seeded organization.settings.manage

## Request contract

**Path/query/header:** None

**Body/DTO:** `UpdateOrganizationRequest(string DisplayName); route: tenantId: Guid`

**JSON example:**

```json
{
  "displayName": "NovaTech Global"
}
```

## Response contract

200 OK: OrganizationResponse(Guid Id, string Slug, string DisplayName, string Status, DateTimeOffset CreatedAt)

**Expected failures:** 400 invalid name, 401/403, 404 foreign tenant.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. DisplayName trimmed 1..200; tenant active and target is same path tenant.
2. Slug and lifecycle Status are not editable through this request.

## Business logic

1. Validate current authorization and tenant scope, update DisplayName and UpdatedAt.
2. Append tenant audit with old/new display names (non-secret).

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement UpdateOrganization command and explicit property update.
4. Prevent mass assignment of creator/status fields.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Authorized owner can rename; updated name reflected on GET.
2. Other tenant user/admin and guest cannot rename.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
