---
TaskId: ID-046
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: GET
Route: "/api/v1/platform/security-events"
---

# ID-046 - Search platform security events

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `GET /api/v1/platform/security-events`

**User story:** US-046: As a platform security admin, I can inspect incidents and privileged changes.

**Actor:** Platform security admin

**Authentication/authorization:** platform.audit.read

**Dependencies:** ID-041, platform audit writer

## Request contract

**Path/query/header:** fromUtc?: DateTimeOffset, toUtc?: DateTimeOffset, actorUserId?: Guid, targetTenantId?: Guid, eventCode?: string, cursor?: string, pageSize?: int=50

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: CursorPage<PlatformSecurityEventResponse> where PlatformSecurityEventResponse(Guid Id, DateTimeOffset OccurredAt, string EventCode, string TargetType, Guid? TargetId, Guid? TargetTenantId, Guid? ActorUserId, bool Success, Guid? CorrelationId, JsonElement Details)

**Expected failures:** 400 bad filters/cursor, 403 unauthorized.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. fromUtc <= toUtc, range <=90 days per query, pageSize 1..100, valid opaque cursor.
2. Caller active and authorized platform auditor; no tenant roles accepted.

## Business logic

1. Query shared.PlatformSecurityAuditEvents by time and filters, keyset pagination.
2. Redact details, never return password/token or broad tenant business data.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement platform audit query using established cursor convention.
4. Use append-only read-only projection and permission policy.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Unauthorized tenant owner receives 403.
2. Security events are chronological and token-free; filters/cursor work.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
