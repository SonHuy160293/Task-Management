---
TaskId: ID-040
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: GET
Route: "/api/v1/organizations/{tenantId}/security-events"
---

# ID-040 - Search tenant security audit events

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `GET /api/v1/organizations/{tenantId}/security-events`

**User story:** US-040: As a tenant security administrator, I can investigate membership and authorization changes.

**Actor:** Organization owner/security admin

**Authentication/authorization:** organization.security_audit.read (organization scope), not included in ordinary admin by default

**Dependencies:** ID-034 and audit writer installed; RLS read strategy

## Request contract

**Path/query/header:** fromUtc?: DateTimeOffset, toUtc?: DateTimeOffset, actorUserId?: Guid, eventCode?: string, cursor?: string, pageSize?: int=50

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: CursorPage<SecurityEventResponse> where SecurityEventResponse(Guid Id, DateTimeOffset OccurredAt, string EventCode, string TargetType, Guid? TargetId, Guid? ActorUserId, bool Success, Guid? CorrelationId, JsonElement Details)

**Expected failures:** 400 date/cursor, 403 permission, 404 foreign tenant.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. fromUtc <= toUtc and range <=90 days per request; pageSize 1..100; cursor opaque signed or validated, eventCode <=100.
2. Tenant active, actor has security audit read permission; no user-provided tenant scope override.

## Business logic

1. Read dbo.SecurityAuditEvents tenant-bound, keyset paginate (OccurredAt DESC, Id DESC).
2. Redact secret/password/token/PII-heavy Details and enforce maximum JSON field size; no audit mutations.
3. RLS policy must allow tenant read under transaction context.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement QueryTenantSecurityEvents and cursor encoding with index-friendly projection.
4. Never let ordinary member or guest access security events.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Cross-tenant access denied, cursor cannot select other tenant events.
2. Results ordered and paginated correctly, redacted.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
