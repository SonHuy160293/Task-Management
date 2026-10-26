---
TaskId: ID-017
Status: Pending
Priority: P0
Phase: "B Organizations"
Method: GET
Route: "/api/v1/organizations/{tenantId}"
---

# ID-017 - Get organization summary

**Status: Pending** | **Priority: P0** | **Phase: B Organizations**

**Endpoint:** `GET /api/v1/organizations/{tenantId}`

**User story:** US-017: As an employee, I can view the identity of my current organization.

**Actor:** Active organization member

**Authentication/authorization:** Active membership + organization.profile.read OR limited guest equivalent

**Dependencies:** ID-015, tenant authorization checks

## Request contract

**Path/query/header:** tenantId: Guid (UUIDv7)

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: OrganizationResponse(Guid Id, string Slug, string DisplayName, string Status, DateTimeOffset CreatedAt)

**Expected failures:** 400 UUID, 401, 403, 404 tenant hidden.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Route tenantId is valid UUID; tenant active; actor membership active.
2. Enforce organization-scope permission or explicit guest-read-limited policy.

## Business logic

1. Resolve tenant from path, bind DB/RLS context from verified membership.
2. Return only profile fields; do not expose billing or settings not present in ERD.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement GetOrganization query, validator and permission-aware handler.
4. Share reusable tenant context resolver with other endpoints.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Authorized member receives only their organization; foreign org =>404.
2. Suspended membership/tenant =>403, guest receives limited public fields only.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
