# Identity Module - Codex execution guide

> **The only unit of tracked implementation work is one endpoint.** One Codex run selects one `ID-###` card and implements exactly one `HTTP_METHOD /api/v1/...` route. Cross-cutting shared code is allowed only to support that selected endpoint. Do not combine two cards in one run.

## 0. Source authority and scope

Before editing, Codex must read, in this order:

1. Repository root `AGENTS.md`, `ARCHITECTURE.md`, `.editorconfig` and applicable module-level `AGENTS.md` files.
2. The actual installed `add-cqrs-api-feature` skill (`SKILL.md`) from the user; **do not assume the DevFlow.Tasks starter paths describe the Identity module**.
3. `docs/identity/README.md`, `API_CONVENTIONS.md`, `PERMISSION_CATALOG.md`, `BACKLOG.md` and **only the selected `tasks/ID-###-...md`**.
4. `docs/identity/reference/identity_erd_postgresql18.md`, `identity_postgresql18.sql` and `Identity_EF_Core_Models_Codex_Implementation_Plan.md` as needed for relevant columns/constraints.
5. Existing Identity Domain/Application/Infrastructure/API project code, DI registrations, pipeline behaviors, EF mappings, modules/contracts and existing tests (if present).

**Trust precedence:** repository constraints/AGENTS.md > actual current source signatures > approved PascalCase EF model mapping specification > reference SQL for semantics/constraints > task contract proposals. If two approved sources genuinely conflict, **stop that task and report the conflict** rather than inventing behavior. This package creates no migrations and makes no claim that the database exists.

## 1. Preconditions before any endpoint can be marked Completed

These are **entry gates, not tracked endpoint tasks**. They must be established using existing repository work, a separately authorized setup change, or the minimal infrastructure explicitly required for the selected endpoint.

- [ ] Actual Identity module, controller base and current auth setup located; .NET solution/source structure is known.
- [ ] All needed EF entities/configurations mapped correctly using PascalCase physical identifiers, `shared`/`dbo` schemas, UUIDv7 keys; real PostgreSQL schema made available separately (do not run/create migrations as side effect of endpoint task unless user authorizes).
- [ ] Actual `IUnitOfWork` and transaction pipeline available (the uploaded skill notes it is constructor-injected even when processing queries). No dummy unit of work; handler/repository never commits with SaveChangesAsync.
- [ ] For every `dbo` command **and read query**, a secure tenant RLS approach exists: reliable tenant claim/membership validation, `SET LOCAL app.tenant_id` in the same DB transaction and a non-`BYPASSRLS` connection role. The skill says the generic CQRS transaction behavior only starts transactions for commands: **fix/plan tenant read strategy explicitly before delivery**.
- [ ] `GET /me/organizations` has a narrowly privileged, user-constrained cross-tenant discovery approach rather than arbitrary tenant RLS bypass.
- [ ] Seeded global Roles/Permissions/RolePermissions meet `PERMISSION_CATALOG.md` codes needed by the selected endpoint, with UUIDv7 PK, scope_kind composite compatibility, `IsActive` and `IsAssignable` checks.
- [ ] Account password hashing (Argon2id), clock, CSPRNG, token hashing, JWT issuing+verification, cookie/CSRF handling, shared rate limiting, audit writing and exception/Result mapping exist if used by selected endpoint.
- [ ] Reliable email notification integration exists when the selected endpoint actually delivers a reset, invitation, verification, or other notification. ID-001 uses the existing reliability outbox and Notification delivery foundation; verify the complete flow for each delivery use case.
- [ ] Project/Team/Performance module can create/archive `AccessScopes` via approved in-process contract/event integration before grant endpoints depend on those scopes.
- [ ] Platform root role initial assignment has a secure out-of-band bootstrap; never create public platform admin onboarding API.
- [ ] Confirm the project's expected verification mode; do **focused** acceptance validation consistent with uploaded skill. Do not run a giant automatic iteration loop unless explicitly authorized.

**If a gate blocks an endpoint:** leave `Status: Pending`, report precise blocker, do not fake implementation or success. A prerequisite is not a new task/endpoint in this backlog.

## 2. CodeGraph-first workflow (mandatory when installed)

1. Confirm the CodeGraph MCP server/index are installed and live (`.codegraph/` may be present). Current public CodeGraph documentation indicates Codex CLI integration through `npx @colbymchenry/codegraph` and the default MCP tool `codegraph_explore`. Do not assume it is configured merely because the docs mention it.
2. Ask **specific** graph questions before editing:
   - `Locate DevFlow.Identity API controllers, PublicApiController/ApiController, ISender, CQRS request/handler/validator signatures, DI registration and error mapping.`
   - `Trace IUnitOfWork, TransactionPipelineBehavior, Identity DbContext, EF repositories and audit interceptor; find the save boundary.`
   - `Trace authentication claims, current user/session, tenant context, RLS connection/transaction setup, permission handlers, and any call sites.`
   - `Show callers/callees and impact radius of the selected endpoint's controller/handler and nearby tests.`
3. Read the actual returned source lines; when changes span multiple classes, inspect the blast-radius report and narrow dependencies. Don't automatically copy symbol names from this guide if source differs.
4. If CodeGraph index is stale, update it using supported installed CodeGraph integration/CLI instructions; re-explore changed code after editing when useful. Do not invent unsupported flags.
5. If CodeGraph is unavailable, tell the user and leave a concrete setup blocker before claiming you complied with the CodeGraph requirement. You may use standard search for diagnosis, but not silently mark CodeGraph-based implementation Completed.

**Reference:** https://colbymchenry.github.io/codegraph/reference/mcp-server/ and https://colbymchenry.github.io/codegraph/reference/integrations/.

## 3. Mandatory `add-cqrs-api-feature` implementation contract

Read the **uploaded user skill itself** each run. Its current contract is:

- API Controller (existing appropriate base) translates HTTP request and calls `DevFlow.SharedKernel.Cqrs.ISender`; no DbContext/repository/identity manager injection into controllers.
- Follow [Modules API conventions](../../src/Modules/AGENTS.md#api-request-contracts-and-failure-handling) for request-model files and centralized failures; inspect [existing registration failure propagation](API_CONVENTIONS.md#existing-registration-failure-path) before simplifying AuthController.
- Application message uses `DevFlow.SharedKernel.Abstractions.Message.ICommand` / `IQuery`; handler uses corresponding `ICommandHandler` / `IQueryHandler`, validator uses `IValidator` and `ValidateAsync`; read **real interface signatures** before implementing.
- Use `Result` / `Result<T>` from `Abstractions.Shared` and existing response/error middleware; no new MediatR, mapping library, second CQRS implementation or invented permission attributes.
- Use sealed request records/messages and sealed validators/handlers where consistent. Carry `CancellationToken` through async calls.
- Application defines repository/integration interfaces; Infrastructure implements. Domain references BuildingBlocks only. EF DTOs and API response models stay separate from entities.
- Inspect existing `EfRepositoryBase<TEntity,TId,TDbContext>` and `IRepositoryBase<TEntity>`. The helper is **not** automatically the repository contract; implement/register required members. `Query()` no-tracking; `TrackedQuery()` tracks.
- Preserve pipeline behavior order: validation -> logging -> performance -> tracing -> transaction. A real `IUnitOfWork` is mandatory; no `SaveChangesAsync` in handlers/repositories and no dummy implementation.
- Controllers must not become public merely to avoid missing auth services. Register real authentication/authorization before applying policies and use existing PublicApiController only for genuinely anonymous endpoints.
- No Dapper/read-repository duplication unless existing pattern or selected endpoint truly needs it. No speculative packages, transport abstractions, controllers or global architecture changes.
- `CreatedAtAction` must reference an already implemented action route. If the GET route is future work, return 201 with a valid Location using an alternative supported by the project.
- Skill default targeted verification: build affected projects and `scripts/Test-Architecture.ps1` when present, plus meaningful focused behavior tests. Follow a stricter/no-verification user instruction if explicitly provided. Report checks actually executed, without pretending a live database or auth setup was verified.

**Do not code the endpoint as `DevFlow.Tasks`** just because the skill's starter example uses Tasks. Discover and target `DevFlow.Identity` or the actual Identity module assembly.

## 4. Single-task execution loop

1. **Pick one Pending task** from [BACKLOG.md](BACKLOG.md) whose dependencies and infrastructure gates can be satisfied. Respect P0 first when feasible; do not assume numeric ID order is dependency order.
2. **Start Codex with the prompt** from [CODEX_PROMPT.md](CODEX_PROMPT.md), substituting the exact task file/ID. Do not paste all 51 cards into Codex context.
3. **Discover actual code with CodeGraph** and invoke your `add-cqrs-api-feature` skill. Identify one route contract, message type, handler, validation, repository, auth policy, existing exception mapping and audit effect.
4. **Implement only this route.** Create/reuse the required CQRS command or query, request/response DTOs, validator, handler, Application abstractions, Infrastructure persistence, DI registration and focused tests. Shared services may be introduced when required *for this endpoint*, not as broad refactors.
5. **Honor ownership**: auth/account and tenant membership data in Identity; project/team/review existence and visibility in owning modules. Avoid cross-module EF foreign keys and don't create external HTTP calls within a modular monolith.
6. **Verify focused behavior** for positive, validation, 401/403, IDOR/cross-tenant, state/conflict, concurrency and audit cases specified on the card. Do not run migrations unless specifically authorized.
7. **Report** endpoint implemented, changed files, codegraph symbols examined, request/response contract, validation/security behavior, constraints encountered, check results and blockers.
8. **Mark Completed only if truly done**: all card checks, live relevant registrations/persistence/authorization, and requested verification pass. Change both `Status: Pending` in YAML frontmatter and visible bold status to `Completed`, and change this task's row in `BACKLOG.md` from `Pending` to `Completed`. Leave all other cards unchanged.
9. **Stop after one endpoint**. Next Codex run selects a different Pending card. Never auto-implement task dependencies by adding their endpoint handlers in the same run.

## 5. Suggested dependency-aware delivery order

**Authentication foundation:** ID-001 -> ID-002 -> ID-004 -> ID-005 -> ID-006. ID-003, ID-007 -> ID-008 and ID-009, ID-010 can follow when their prerequisites exist. ID-011..014 are independent P1 convenience paths after login/session foundation.

**First tenant:** ID-015 -> ID-017 -> ID-016, ID-019. Seed role+permission definitions and tenant/RLS integration *before* role-related work. Organization admin functions ID-018, ID-020..023 and ID-051 can proceed after their prerequisites.

**Scoped roles before invitations:** ID-030 -> ID-033 -> ID-034 -> ID-035, then ID-024 -> ID-025 -> ID-026 -> ID-028. Alternative is to build create invitation first and accept only once role assignment is ready. ID-027 and ID-029 are P1 refinements.

**Direct permission overrides:** ID-032 -> ID-036 -> ID-037 -> ID-038 -> ID-039; ensure grants cannot overrule guest/business visibility.

**Governance:** ID-040 tenant audit after tenant operations; ID-041 -> ID-047 -> ID-048 with platform bootstrap, then ID-042 -> ID-043 and ID-044 -> ID-045, ID-046. Add ID-049 and ID-050 for platform user/privilege discovery. Platform tasks may be moved earlier for operational needs but must never rely on unseeded platform grants.

## 6. Per-task completion definition

A task is **Completed** only when all apply:

- Exactly **one** specified HTTP method/route is present and reachable through correctly registered DI, auth and persistence.
- Explicit request DTO, response DTO, route/query fields and status mapping agree with task card (or documented approved contract change).
- Validation is enforced server-side; handler cannot bypass security by trusting passed IDs/claims alone.
- New endpoint respects user/account/tenant/membership/scope/guest rules, and additional owning-module resource conditions when applicable.
- Concurrency/transactions are correct for refresh rotation, invitations, unique grants and last-owner operations; a dangerous race is not hand-waved away.
- Credential/token values are hashed at rest, never logged/returned accidentally; secret-bearing responses/cookies follow specified rules.
- Audit and cache invalidation are included for security-sensitive changes.
- Focused negative and positive acceptance checks are executed, and checks and limitations are truthfully reported; if repository instructions say no verification, mark results as not executed and **do not** assert production readiness.
- Only that card and its backlog row change to `Completed`; no unrelated endpoint is implemented.

## 7. Risk register / open architectural decisions

| Risk | Why it matters | Resolution before production |
|---|---|---|
| RLS on queries | `SET LOCAL` needs DB transaction, CQRS generic query path may have none | Design connection/transaction scope or narrowly scoped safe alternative; test tenant A/B read isolation |
| Email delivery | Each delivery use case needs atomic staging and durable processing | Reuse the reliability outbox and verify the full delivery flow |
| Permission seeding | ERD catalogs exist but no required codes/role relationships guaranteed | Approve `PERMISSION_CATALOG.md`, seed out of band, enforce ScopeKind and delegation |
| Scope ownership | Logical project/team/performance resource may not exist/archived | Establish in-process owner-module contract/events with transactional/compensating behavior |
| JWT stale permissions | Revoked/suspended member may retain old access token | Reauthorize tenant/scope on each request, invalidate effective caches, check session/global status |
| Refresh rotation races | Parallel submissions and replay may produce split successors | Lock token/session state, consume once and revoke family on replay |
| Browser token storage | Cookies alone do not eliminate CSRF, especially cross-site | Confirm same-site SPA/BFF, cookie Path, Origin/CSRF, CORS and HTTPS deploy topology |
| Membership offboarding | Last owner or platform recovery admin can be locked out | Lock and enforce minimum surviving administrators under concurrency |
| PascalCase schema | Existing SQL reference snake_case vs previous model plan PascalCase | Use a single approved mapping/migration casing convention before endpoint tests |
| Sensitive performance data | A raw grant is not sufficient to read someone else's review | Performance module enforces reviewer relationship, review stage and data confidentiality |

## 8. Task state example

**Before:**

```yaml
TaskId: ID-004
Status: Pending
```

**After real completion:**

```yaml
TaskId: ID-004
Status: Completed
```

Also update the corresponding BACKLOG row and visible status in the selected card. Do **not** mark everything Completed because the plan was generated or an agent responded positively.
