# Identity API conventions and invariants

These conventions apply to every [endpoint task](BACKLOG.md). Individual cards specify more precise rules.

## HTTP / DTO contract

- Routes versioned `/api/v1`; one endpoint task = **one HTTP method + route**, not one controller/file.
- JSON uses the **existing ASP.NET Core JSON policy**; this proposal shows camelCase examples while C# request/response records use PascalCase properties.
- Keys use `Guid` UUIDv7 (`Id`, `TenantId`, `UserId`, `ScopeId`, `MemberId`); `DateTimeOffset` with UTC timestamps. Values in permissions/roles/status strings preserve **lowercase** codes.
- HTTP request-model placement and failure propagation follow [Modules API conventions](../../src/Modules/AGENTS.md#api-request-contracts-and-failure-handling). Apply them to every endpoint, including command and query endpoints.
- GET/DELETE normally have **no request body**. Query params are explicit in the task card.
- HTTP: `200` read/update result, `201` created resource, `202` generic asynchronous request, `204` successful no-body action; `400` invalid input, `401` unauthenticated/invalid credentials, `403` authenticated but not allowed, `404` hide inaccessible tenant-owned target, `409` state conflict, `410` invalid/expired one-time token, `429` rate limit.
- Response DTOs never contain password hash, raw/hashed refresh token, OTP hash, invitation token, or internal audit-only metadata. No EF entities are API contracts.
- `201 Created` with `Location` must point to an **implemented** GET endpoint. While a GET has not been implemented, use a standards-compliant URI in a `Created(...)` response rather than an invalid `CreatedAtAction`.
- Pagination: ordinary lists use `PagedResponse<T>(Items,Page,PageSize,TotalCount)` with pageSize<=100; audit uses opaque cursor and stable `(OccurredAt,Id)` keyset pagination. Bound all search and date ranges.

### Existing registration failure path

`AddApplicationPipelineBehaviors` registers `ValidationDefaultBehavior`, which
propagates validator failures as `CqrsValidationException` before the transaction.
The alternate `ValidationPipelineBehavior` returns failed results but is not the
registered validation behavior. Inspect the active DI registration when choosing
failure propagation.

`RegisterAccountHandler` throws `ValidationException` for passwords in the local
common-password list. `GlobalExceptionHandlingMiddleware` produces the 400 error
envelope and trace ID. Redis registration protection can independently return 503.
`AuthController` maps the request from `Contracts/Auth/RegisterRequest.cs`,
sends the command, and returns 202 for a successful result. Handler failures throw
inside the command transaction so the unit of work rolls back before the
middleware writes the response.

## Identity and session protections

- Account creation is unverified by default; no access token until email proof completed.
- Email lookup/unique index is case-insensitive, but do not use unsafe hand-built SQL interpolation. Password hashes = Argon2id PHC; use an injectable password hasher with bounded memory/time cost and rehash support.
- Registration accepts 15–128 Unicode scalar values, rejects the seven locally listed common passwords, and imposes no character-composition rule. This limited list does not provide broad breached-password screening. Review the desired blocklist before implementing password changes or resets; reject oversized HTTP payloads early.
- Login access JWT contains `sub`, `sid`, `iss`, `aud`, `iat`, `exp`, `jti` (as supported by existing auth architecture). Avoid embedding tenant roles/direct permissions that go stale. API must revalidate session/user and requested tenant membership at an appropriate freshness level for required immediate revocation.
- Refresh token = >=256-bit CSPRNG opaque random, SHA-256 digest in `bytea` exactly 32 bytes; keep raw token only in `Secure`, `HttpOnly` refresh cookie (same-site SPA model, `SameSite=Strict`, bounded `Path=/api/v1/auth`). Require trustworthy Origin/CSRF controls; for cross-site frontends, explicitly redesign cookie/CSRF policy rather than disabling checks.
- Password reset/email verification/invitation tokens are independently generated high-entropy, expiring, hash-only-at-rest, consumed once with atomic transaction. Avoid putting tokens in logs or long-lived URLs (client can parse a fragment and submit token in POST body).
- The frontend cannot be trusted to log out devices simply by deleting browser state: server-side session revocation, expiry and refresh-replay detection are mandatory.
- Limit password/login/registration/reset/invite brute-force and enumeration with shared throttling. Retry and failure statuses must not reveal account existence for register/resend/forgot workflows.

## Organization and guest protection

- `shared.Users` account = global identity. `dbo.OrganizationMembers` = tenant membership. A globally active user may hold different roles in A/B.
- Every tenant endpoint binds tenant context from the **verified route `TenantId`** plus active membership; never rely solely on JWT role claims or untrusted HTTP headers. Platform admin is not automatically a tenant member.
- Organization scope is `(TenantId, scope_kind=organization, ResourceId=TenantId)`. Project/team/performance scopes exist only after their owner module authoritatively registers the resource ID. Do not implement public generic scope creation in Identity.
- Active permission = active user AND active tenant AND active membership AND active target scope AND guest hard restrictions AND (`role grants` UNION `direct permission grants`) at exact scope AND applicable business rules in owning module.
- **No automatic organization->project/team/performance permission inheritance.** An organization-scoped `project.read_all` (or administrative delegation permission) is a separate capability that owning module must check explicitly.
- A Guest is an invariant identity classification. Even a mistakenly assigned sensitive role/direct grant does not open HR reviews or tenant-wide membership/audit data.
- Last active organization owner remains present after simultaneous suspend/remove/role revoke requests. Assign owner to an active member first (by owner-authorized flow). Lock when computing last-owner invariant.
- Removing membership does **not** delete shared global user or audit. Re-inviting a removed user reuses/reopens membership but must not reactivate revoked historical access.

## RBAC and direct grants

- `shared.Roles`, `shared.Permissions`, `shared.RolePermissions` are seeded **global** catalogs; no arbitrary tenant-defined role CRUD in this version.
- `dbo.MemberRoleAssignments`: exactly one role+scope+member; `dbo.MemberPermissionGrants`: single direct **ALLOW** grant with justification and optionally expiry. Both use composite tenant and scope-kind referential constraints.
- Assignment permission is **not** the same as grant delegation: check who can delegate this **specific** role or permission at **this** scope. Prevent self-elevation, guest escalation, assigning platform roles through tenant API, and arbitrary owner promotion by organization admins.
- Partial unique indexes use `RevokedAt IS NULL`: an **expired but unrevoked** row still prevents a new identical grant. Explicitly revoke old row or renew it atomically before creating a new one.
- Caches of effective grants MUST have reliable invalidation/versioning for suspend, remove, revoke, permission definition deactivation and resource archival; otherwise do not cache effective access.

## PostgreSQL and modular monolith integration

- Physical schema `shared` versus `dbo` reflects global versus tenant-owned records. PostgreSQL RLS uses `SET LOCAL app.tenant_id` **inside the same DB transaction** for each tenant query/command. `FORCE ROW LEVEL SECURITY` prevents owner bypass, subject to correct DB role and privileges.
- The provided DevFlow skill's transaction behavior opens a transaction on **commands only**. Consequently, **read queries require an explicitly designed tenant transaction/connection strategy** before RLS is enabled for production. This is a hard stop, not permission to bypass RLS.
- `GET /me/organizations` is a special self-membership discovery across tenants; use a narrowly privileged, logged server-side pattern constrained to the authenticated global UserId, not a user-controlled arbitrary `TenantId` or general table-owner session.
- `AccessScope.ResourceId` is a **logical** cross-module reference, not an FK. Owning Project/Team/Performance modules validate resource existence and lifecycle. In a modular monolith use in-process application contracts or domain events, not an unnecessary inter-module HTTP call.
- Avoid writing unrelated migrations in a one-endpoint task. Existing `shared.Users` etc. mappings must match the PascalCase EF specification. Database provisioned schema/role seed/real IUnitOfWork are entry gates.
- Security audit is append-only, with actor/target and a non-secret reason in bounded JSON. Preserve correlation IDs but do not confuse correlation GUID with mandatory UUIDv7 PK.
