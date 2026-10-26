---
TaskId: ID-025
Status: Pending
Priority: P0
Phase: "C Invitations"
Method: GET
Route: "/api/v1/organizations/{tenantId}/invitations"
---

# ID-025 - List pending and past invitations

**Status: Pending** | **Priority: P0** | **Phase: C Invitations**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/invitations`

**User story:** US-025: As an organization admin, I can track outstanding and accepted invitations.

**Actor:** Organization admin/owner

**Authentication/authorization:** organization.members.invite or organization.invitations.read

**Dependencies:** ID-024

## Request contract

**Path/query/header:** status?: pending|accepted|revoked|expired; page?: int=1; pageSize?: int=20

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: PagedResponse<InvitationResponse> with InvitationResponse(Guid Id, string InviteeEmail, string MembershipType, string Status, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, DateTimeOffset? AcceptedAt)

**Expected failures:** 400 filters, 403 unauthorized, 404 foreign tenant.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Allowed statuses only; pageSize 1..100; tenant must be active.
2. Guest/outsider denied.

## Business logic

1. Filter dbo.Invitations by tenant, sort CreatedAt DESC plus Id; do not expose TokenHash or raw token.
2. Normalize stale pending expiration state through a safe background/update process before relying on partial unique index.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListInvitations query and projection.
4. Prefer no-tracking and tenant-scoped pagination; do not mutate in GET.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Admin sees only invitations in own tenant with correct filters.
2. Token fields never serialized; guest denied.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
