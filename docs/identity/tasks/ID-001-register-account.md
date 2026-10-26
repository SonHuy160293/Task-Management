---
TaskId: ID-001
Status: Completed
Priority: P0
Phase: "A Authentication"
Method: POST
Route: "/api/v1/auth/register"
---

# ID-001 - Register account

**Status: Completed** | **Priority: P0** | **Phase: A Authentication**

**Endpoint:** `POST /api/v1/auth/register`

**User story:** US-001: As a new employee or organization owner, I can create an account so I can join or start a workspace.

**Actor:** Anonymous

**Authentication/authorization:** Public; IP/email rate-limited

**Dependencies:** Auth infrastructure, Argon2id, token generator, Identity DB and
IUnitOfWork; existing Identity outbox/worker, RabbitMQ, Notification inbox/delivery
worker, and Notification's public encryption key.

**Current state:** registration stages the pending user, credential, token digest and encrypted email request in one transaction. The existing worker and Notification foundation deliver it at least once. The revised acceptance checks were verified as recorded below.

The user-approved scope in this card supersedes older **ID-001-specific** statements
in the execution guide and prerequisite notes that exclude email delivery or an
outbox. General repository and module architecture rules still apply.

## Request contract

**Path/query/header:** None

**Body/DTO:** `RegisterRequest(string Email, string Password, string DisplayName, string? Locale, string? TimeZone)`

**JSON example:**

```json
{
  "email": "alice@example.com",
  "password": "a long unique passphrase 2026",
  "displayName": "Alice Nguyen",
  "locale": "en",
  "timeZone": "Asia/Ho_Chi_Minh"
}
```

## Response contract

202 Accepted: RegistrationAcceptedResponse(string Message) { Message = "If eligible, your registration request has been accepted." }

**Expected failures:** 400 validation, 429 throttle, 503 if a required security dependency is unavailable.

Use repository `Result` / `Result<T>` and existing HTTP error mapping; response model is an Application/API DTO, never an EF entity. `Guid` represents UUIDv7 primary keys; timestamps are UTC `DateTimeOffset`. Existing project JSON naming policy controls PascalCase C# to JSON serialization.

## Validation rules

1. Trim and normalize email for lookup; valid address, length 3..320; never silently strip password whitespace.
2. DisplayName trimmed length 1..160; Locale <=16 valid language tag; TimeZone <=64 valid IANA zone.
3. Password 15–128 Unicode scalar values; reject the existing seven-entry local common-password list, without a complexity composition rule. This list does not provide broad breach screening.
4. Rate-limit by IP and normalized email; bounded payload.

## Business logic

1. Validate and rate-limit the request, apply the local password policy, and use
   the existing case-insensitive registration lookup and transaction-scoped
   concurrency control.
2. For a new email, stage the pending user, Argon2id credential, verification-token
   digest, and **one** encrypted verification-email outbox message in the same
   Identity unit-of-work transaction. Generate a stable UUIDv7 message ID once
   for the request. The existing transaction pipeline owns the single save and
   commit; the handler and outbox adapter do not commit or call `SaveChangesAsync`.
3. A failure before commit, including encryption or outbox staging failure, rolls
   back all four records. The worker sees no uncommitted message. Do not publish to
   RabbitMQ or call the email provider from the registration transaction.
4. For an existing email, including a concurrent case-variant request, retain the
   indistinguishable `202` response and existing credential. Create no additional
   user, credential, token, or outbox message. Do not disclose account existence.
5. Do not issue access/refresh tokens until verified. The Identity token table
   stores only the token digest; the outbox and broker carry the raw token and
   recipient only inside the encrypted payload. The provider receives rendered
   email after Notification decrypts it. Never log a password, token, recipient,
   plaintext payload, or transport exception containing sensitive data.

## Delivery workflow

Reuse `EmailDeliveryRequestedV1`, `IVerificationEmailOutbox`, Identity's existing
outbox relay, and Notification's inbox, delivery-job, receipt and provider flows.
Add an Application-owned request-factory abstraction backed by Infrastructure's
existing `VerificationEmailEnvelopeFactory`. Pass the generated token and its UTC
creation/expiry into the encrypted request; preserve a stable message ID and
immutable payload across retries. Register the factory and outbox adapter in the
Identity API without starting a broker consumer or outbox relay in that host.

The Identity API needs Notification's configured **public key and key ID** to
encrypt a request. Provision and validate these inputs for local Docker, IDE and
server operation; missing or invalid key configuration must fail clearly. Stage
only public key material into the Identity API's read-only runtime mount. Keep
private keys in Notification and retain old private keys while messages encrypted
with them remain outstanding. The public key is an independently provisioned
contract input: neither module's startup or database jobs require the other
module's database.

Identity workers claim committed outbox rows with the existing PostgreSQL
`FOR UPDATE SKIP LOCKED` transaction, expiring leases and fencing tokens. One
database lease is current for a row at a time. A worker publishes outside the
claim transaction and marks the row published only after broker confirmation and
only while it owns the current lease. After an expired lease, another worker may
reclaim the row; a stale worker cannot update its state. Existing receipt
reconciliation requeues messages whose durable Notification receipt is missing.

Publication is **at least once**, not exactly once. A worker can publish and crash
before recording confirmation, or complete an in-flight publish after its lease
expires. Retries retain the same message ID and payload. Notification deduplicates
by message ID and immutable payload hash, creates one durable delivery job, and
rejects a conflicting payload under the same ID. A duplicate may reissue a receipt.
Notification's provider retries and idempotency reduce duplicate sends, but neither
provider acceptance nor actual email delivery can be guaranteed exactly once.

### Sequence diagram

```mermaid
sequenceDiagram
    participant C as Client
    participant I as Identity API
    participant IDB as Identity DB
    participant W as Identity worker
    participant Q as Message queue
    participant N as Notification service
    participant NDB as Notification DB
    participant P as Email provider

    C->>I: POST /register
    I->>IDB: Begin transaction and check email
    alt New account
        I->>I: Generate token and encrypt email request
        I->>IDB: Stage user, credential, token digest, outbox message
        IDB-->>I: Commit all changes
    else Existing account
        IDB-->>I: Complete without a new outbox message
    end
    I-->>C: 202 Accepted

    loop Background relay
        W->>IDB: Claim committed message with lease
        W->>Q: Publish encrypted message with stable ID
        Q-->>W: Publisher confirmation
        W->>IDB: Mark published using lease token
    end

    Q->>N: Deliver message
    N->>NDB: Deduplicate; commit inbox, email job, receipt
    N-->>Q: Acknowledge message
    N->>P: Submit email from durable job
    P-->>N: Accept or return failure
    N->>NDB: Record outcome or schedule retry
    N->>Q: Publish durable receipt
    Q->>W: Deliver receipt
    W->>IDB: Record receipt
```

The diagram shows the successful path. Broker retries, missing-receipt
republication, duplicate consumption, lease recovery, and provider failure are
asynchronous branches governed by the existing durable-delivery implementation.
An HTTP `202` confirms registration acceptance; it does not confirm provider
acceptance or delivery to an inbox.

## Implementation steps (one endpoint only)

1. Read `docs/identity/EXECUTION_GUIDE.md`, the uploaded `add-cqrs-api-feature` skill, applicable `AGENTS.md`/`ARCHITECTURE.md`, and the current endpoint card.
2. Inspect the existing registration handler, transaction/unit-of-work,
   `IdentityEmailOutbox`, envelope factory, relay and Notification intake. Use
   CodeGraph when the repository is indexed; otherwise follow root `AGENTS.md`.
3. Add the Application-owned encrypted-request factory seam and its Infrastructure
   adapter, configure public-key distribution and validation, and register the
   factory and existing outbox adapter for the API independently of worker transport.
4. Stage one request only in the new-account branch of the existing registration
   transaction. Preserve the case-insensitive unique-email/concurrency behavior,
   existing request/response contracts, and centralized failure handling.
5. Exercise the acceptance checks with isolated infrastructure, including scaled
   relay workers, encrypted end-to-end delivery, failure recovery and privacy.
   Update the implementation and deployment documentation after behavior is verified.

## Acceptance checks

1. An isolated PostgreSQL test proves the pending user, PHC-only credential, one
   unused token digest and one encrypted outbox message commit together. Inject an
   outbox/encryption failure and prove none of the four persists.
2. Concurrent duplicate/case-variant requests create one user and one corresponding
   outbox message; existing credentials remain unchanged and responses remain
   indistinguishable. Weak password, invalid timezone/email, rate limiting, and
   Redis-unavailable handling preserve the existing HTTP behavior.
3. Run at least two Identity relay instances against the same database. Verify
   exclusive current claims, expired-lease recovery, and rejection of stale lease
   completion. Simulate a crash after broker confirmation and a missing receipt;
   retries preserve message ID and payload.
4. Verify Notification persists one inbox/job for repeated deliveries of the same
   ID and payload, and rejects a different payload under that ID. Exercise receipt
   reissuance/reconciliation and provider retry behavior.
5. Register through the HTTP endpoint on an isolated RabbitMQ/Mailpit deployment.
   Verify the token in the captured email hashes to the Identity token digest;
   persisted outbox/inbox payloads contain neither plaintext token nor recipient.
   Check registration and delivery logs and HTTP responses for sensitive values.
6. Verify missing/invalid public-key configuration fails clearly, Identity's API
   runtime receives only public material, and selected module startup does not
   require the other module's database. Test local Docker, IDE and server config.
7. Run relevant Identity/Notification tests and the architecture check. Record
   actual evidence and any environment-dependent checks that could not run.

- [x] Exactly the documented single HTTP method + route was added or modified.
- [x] Explicit DTO, validator, handler, repository integration and result mapping follow the existing custom CQRS stack.
- [x] Tenant authorization/guest/security audit and relevant negative cases are checked.
- [x] Relevant targeted verification was actually run and results recorded (if blocked, leave Pending).
- [x] Update this card `Status` to `Completed` AND the same row in `docs/identity/BACKLOG.md` only after all checks pass.

## Codex notes / risks

Dependency implementation and setup instructions: [ID-001 prerequisites](../ID-001_PREREQUISITES.md)
and [Notification delivery foundation](../../notification/README.md). Some older
prerequisite text describes the pre-integration scope; this card's approved delivery
requirements take precedence for ID-001. `TransactionPipelineBehavior` invokes
`IUnitOfWork.ExecuteInTransactionAsync`, which exclusively owns transaction
creation, persistence completion, commit and rollback.

The [earlier acceptance review](../ID-001_PREREQUISITES.md#id-001-acceptance-review)
is historical and does not verify the new registration-to-outbox integration.
The revised acceptance checks are recorded below.

This task specifies exactly **one endpoint** and uses the existing Notification
delivery foundation. Email verification/confirmation and resend endpoints remain
separate tasks. Do not silently change the ERD or add an unrelated endpoint.

## Verification recorded on 2026-10-27

- Identity suite: 38 passed with isolated PostgreSQL 18 and Redis. Registration HTTP validation, duplicate responses, throttling, dependency failure, password policy and key validation are covered.
- Notification real-service suite: 27 passed, 3 skipped (deployment-only checks); isolated PostgreSQL/RabbitMQ/Mailpit exercised deduplication, receipt reissuance, fencing, reconciliation and provider retries.
- Registration transaction tests: 4 focused PostgreSQL checks passed, including the real handler with concurrent case variants, encryption failure and outbox insert failure. Two relay instances shared the database; publication-before-mark was simulated and the recovered ID and payload matched.
- Disposable Docker deployment smoke: 3 passed, including registration over HTTP through RabbitMQ to Mailpit; the captured token hashed to the stored Identity digest. Runtime logs were checked for that test's token, recipient and password; the API secret mount contained the selected public key and no private PEM. Repeated init and a conflicting public key ID were checked.
- Independent Identity startup, IDE settings, real host readiness, and retained-volume restart passed. Server-mode Identity-only image configuration and secret preflight passed; no production broker or server deployment was exercised. Architecture check passed for 24 projects.

Registration creates a global account and no tenant, membership, guest access or new security audit event. Email confirmation, resend, production provider operations and delivery guarantees beyond at-least-once publication are outside this task.
