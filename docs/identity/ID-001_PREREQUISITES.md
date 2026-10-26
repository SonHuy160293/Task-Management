# ID-001 prerequisite setup and readiness

Registration creates a pending global user, an Argon2id credential, an unused verification challenge digest, and an encrypted email request in the same transaction. The raw challenge is included only in the encrypted request. Delivery remains at least once; see the ID-001 card for the completed acceptance record.

## Implemented dependency placement

All module paths below are relative to src/Modules/DevFlow.Identity.

| Layer/folder | Components |
| --- | --- |
| Application/Abstractions/Security | IPasswordHasher, IVerificationTokenGenerator, IRegistrationRateLimiter |
| Application/Security | PasswordPolicy, CommonPasswordBlocklist, RegistrationEmail, VerificationToken, result types |
| Application/Features/Auth/Commands/Registration | RegisterAccountCommand, RegisterAccountValidator, RegisterAccountHandler, RegistrationAcceptedResponse |
| Application/Abstractions/Repositories | Existing user, credential and token contracts; IUserRepository gains ExistsForRegistrationAsync |
| Infrastructure/Security/Passwords | Argon2PasswordHasher |
| Infrastructure/Security/Tokens | VerificationTokenGenerator |
| Infrastructure/Security/RateLimiting | RedisRegistrationRateLimiter |
| Infrastructure/Security/Options | Argon2Options, VerificationTokenOptions, RegistrationRateLimitOptions |
| Infrastructure/DepedencyInjection | Existing AddInfrastructure calls AddIdentitySecurity |
| Infrastructure/Persistence/Core | Existing IdentityUnitOfWork |
| API/Security | RegistrationProtectionMiddleware and RegistrationProtectionExtensions |
| tests/DevFlow.Identity.Tests (repository root) | Security, Registration, Persistence tests |

## Transaction ownership

The existing flow remains controller -> ISender -> validation -> logging ->
performance -> tracing -> TransactionPipelineBehavior ->
IUnitOfWork.ExecuteInTransactionAsync -> handler.

ExecuteInTransactionAsync opens the transaction, invokes the handler, saves once,
and commits. Failure Results and exceptions roll back and clear staged changes.
Cleanup does not replace the original command exception. Handlers and repositories
never call SaveChangesAsync, open transactions, or commit.

ExistsForRegistrationAsync requires this existing transaction. It takes a
transaction-scoped advisory lock using PostgreSQL lower(email), then performs a
parameterized case-insensitive lookup with the same expression as the required
unique index. Credential/token repositories stage changes in the same context.

Automatic EF retries are disabled for Identity: retrying individual operations
conflicts with explicit transactions, while replaying the handler would repeat
secret generation. Transient database failure follows the
existing exception mapping. No hidden command retries are performed.

## Runtime setup

Supply secrets through environment variables or the deployment secret store:

- ConnectionStrings__ApplicationDb: provisioned PostgreSQL 18 database.
- Identity__Security__RegistrationRateLimit__RedisConnectionString: shared Redis.
- Identity__Security__RegistrationRateLimit__KeySecretBase64: 32-64 random bytes,
  Base64 encoded, shared across replicas. Generate once per deployment, not per startup.

Security defaults and bounds are described in SECURITY_SETUP.md. Options validate
at startup. Redis uses bounded timeouts and fails closed with 503. Registration
checks a seven-entry common-password list locally; it performs no external breach
lookup and cannot identify most compromised passwords.

Configure only actual trusted proxy addresses under ForwardedHeaders:KnownProxies.
The registration guard uses the resulting Connection.RemoteIpAddress, not arbitrary
forwarding headers. The existing host still requires its normal authentication,
CORS and other configuration even though registration itself is anonymous.

## Database entry gate

Schema provisioning was excluded from the original endpoint task. The subsequent
[migration runner](MIGRATIONS.md) now supplies the initial PascalCase schema and
required expression indexes, without seed data. Run it before this API. Database
requirements include:

- Users: pending status, UUIDv7-compatible IDs, UTC timestamps, and a valid unique,
  unfiltered single-key expression index on lower("PrimaryEmail").
- PasswordCredentials: one credential per user, restrictive user FK, bounded PHC
  column and existing checks.
- OneTimeTokens: unique 32-byte digest, restrictive user FK, verify_email purpose,
  UTC creation/expiry with expiry after creation.
- PostgreSQL 18 functions used by the mappings: uuidv7 and uuid_extract_version.

Registration touches no tenant-owned dbo tables. This endpoint does not create
memberships or grants. Existing audit timestamps apply; no new global audit event
contract is invented. Credentials and token material are excluded from logging and
response DTOs; command/request/token ToString methods redact secrets.

## Focused verification

Run from the repository root:

    dotnet test tests/DevFlow.Identity.Tests/DevFlow.Identity.Tests.csproj
    powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Architecture.ps1

Set DEVFLOW_TEST_REDIS to an isolated Redis endpoint to enable atomic shared-bucket,
expiry and outage checks. Redis tests use random HMAC keys and expiring counters.
Set DEVFLOW_TEST_POSTGRES to an already provisioned isolated PostgreSQL 18 database
to enable unique-index, concurrent atomic registration and unit-of-work rollback
checks. These tests create test rows with unique random email addresses; committed
test rows are cleaned up by their generated identifiers. They never create schema.
Do not point either variable at production.

HTTP tests use real routing, the custom sender/pipeline, validator, handler and
response envelope, with in-memory doubles for security/persistence. They verify
public registration, generic duplicate responses, unchanged credentials, validation,
throttle/dependency statuses and payload protection. They do not prove PostgreSQL
transaction durability or real deployed authentication configuration.

Historical endpoint-task verification: dependency/HTTP and real Redis tests passed. PostgreSQL tests
were skipped because DEVFLOW_TEST_POSTGRES was not supplied. ID-001 remains Pending
until database-backed acceptance is verified; no acceptance boxes are marked.

## ID-001 acceptance review

This section records the earlier endpoint review, including the external screening
implementation that was subsequently removed; those historical results do not
verify the current local-only password policy. The subsequent 2026-10-25
migration-runner verification supplied an isolated migrated PostgreSQL 18 database:
all 29 Identity tests passed with PostgreSQL and Redis enabled, including the
three previously skipped database checks. See [MIGRATIONS.md](MIGRATIONS.md#verification-recorded-on-2026-10-25).
The Docker host also passed readiness/liveness checks. Live HIBP/email delivery and
production tenant/role controls remain outside that verification; this migration
task does not change endpoint backlog status.

The existing endpoint was inspected again through CodeGraph and the installed
`add-cqrs-api-feature` workflow. No additional endpoint or replacement CQRS stack
is needed. The following checks were executed against the current source:

| Check | Result and evidence |
| --- | --- |
| Affected API build | `dotnet build src/Modules/DevFlow.Identity/DevFlow.Identity.API/DevFlow.Identity.API.csproj --no-restore --nologo --verbosity quiet` succeeded. |
| Focused Identity suite | With `DEVFLOW_TEST_REDIS=127.0.0.1:6379`, `dotnet test tests/DevFlow.Identity.Tests/DevFlow.Identity.Tests.csproj --no-restore --nologo --verbosity quiet`: 26 passed, 3 skipped, 0 failed. |
| Architecture | `powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Architecture.ps1` passed for 14 projects. |
| Acceptance 1: pending user, PHC-only credential, unused challenge, safe response | HTTP tests verify staged pending user/challenge and generic response using persistence/security doubles. Separate real hasher/token tests pass. Actual PostgreSQL persistence remains unverified. |
| Acceptance 2: duplicate/case-variant concurrency, unchanged password | HTTP duplicate response and unchanged credential checks pass; real PostgreSQL concurrent insertion and unique-index checks are skipped. |
| Acceptance 3: validation and throttling | Invalid email, short password, invalid zone, compromised-password rejection, 429/Retry-After and dependency 503 checks pass. Real Redis atomic limits and expiry tests pass. |
| Route and custom CQRS integration | Source inspection confirms only `POST /api/v1/auth/register` in AuthController, explicit DTOs, custom ISender/command/validator/handler, registered repositories and existing response envelope; HTTP test-host routing passes. |
| Authentication/authorization | This task deliberately permits anonymous registration. No 401/403 is required by its contract. The HTTP test host does not verify the deployed host's authentication configuration. |
| Tenant/guest/direct permission isolation | Registration creates a global shared account only. Source inspection confirms no tenant selection, dbo writes, membership, role or direct-permission assignment. Tenant and guest authorization endpoints are outside ID-001. |
| Audit/privacy | Existing timestamp auditing remains registered. The task specifies no new security audit event. Logging tests verify command secrets are excluded, and response tests exclude credential material. |
| Transaction and cancellation | Source inspection confirms API-to-sender cancellation propagation and pipeline-owned ExecuteInTransactionAsync. PostgreSQL success/failure rollback and durable atomicity tests remain skipped. |

The three skipped checks are `FailedResultAndExceptionRollBackWithoutPersistingStagedUser`,
`ConcurrentCaseVariantsCommitOnlyOneAtomicRegistration`, and
`RequiredUniqueEmailIndexExists`. No `DEVFLOW_TEST_POSTGRES` connection was supplied,
and no PostgreSQL service was running during this review. Supply an already
provisioned isolated PostgreSQL 18 database with the required PascalCase tables,
constraints and unique email index, then run the focused suite with that connection.
Schema creation/updates remain excluded from the endpoint task. Full Identity host
startup and a live HIBP request were not tested; HTTP screening uses deterministic
responses. These limitations prevent marking the task Completed.
