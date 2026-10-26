---
TaskId: ID-049
Status: Pending
Priority: P1
Phase: "E Platform and audit"
Method: GET
Route: "/api/v1/platform/users"
---

# ID-049 - Search global user accounts

**Status: Pending** | **Priority: P1** | **Phase: E Platform and audit**

**Endpoint:** `GET /api/v1/platform/users`

**User story:** US-049: As a platform security operator, I can locate an account by verified identifier before performing restricted account actions.

**Actor:** Platform security/support admin

**Authentication/authorization:** platform.users.read; global account metadata only

**Dependencies:** ID-041 and platform.users.read role seeding

## Request contract

**Path/query/header:** email?: string exact match preferred; status?: pending|active|suspended|deleted; page?: int=1; pageSize?: int=20

**Body/DTO:** `None (no JSON request body)`

**JSON body:** None.

## Response contract

200 OK: PagedResponse<PlatformUserSummaryResponse> where PlatformUserSummaryResponse(Guid Id, string PrimaryEmail, string DisplayName, string Status, DateTimeOffset? EmailVerifiedAt, DateTimeOffset CreatedAt)

**Expected failures:** 400 filters, 403 actor unauthorized.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Email lookup if supplied valid <=320; no wildcard email prefix search for non-privileged operators; pageSize 1..100.
2. Requires active platform authorization; never accept tenant-admin role as substitute.

## Business logic

1. Search shared.Users by normalized exact email and/or status with bounded pagination; no password, session or tenant-work-item data.
2. Audit privileged account lookup if policy requires and enforce anti-enumeration through strong platform-only authorization.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Use CodeGraph `codegraph_explore` to find relevant controllers, CQRS contracts, validators, IUnitOfWork, EF mappings, auth and auditing; inspect the result and blast radius before edits.
3. Implement ListPlatformUsers query and safe DTO projection.
4. Use existing platform permission resolver, not tenant membership queries.
5. Add focused tests/checks for the acceptance cases without implementing unrelated endpoints; follow skill verification rules and project constraints.

## Acceptance checks

1. Platform security operator can find known user to suspend; ordinary tenant owner cannot.
2. Response never includes PasswordHash, token digests, full active sessions or tenant business data.

- [ ] Exactly the documented single HTTP method + route was added or modified.
- [ ] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [ ] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [ ] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [ ] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

This task specifies exactly **one endpoint**. Shared infrastructure needed to implement it is allowed only when minimal and directly required. Never create a second endpoint or silently change the ERD.
