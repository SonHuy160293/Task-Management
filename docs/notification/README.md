# Notification delivery foundation

Supported deployment: start with [module onboarding](../../deploy/README.md) and
[deployment operations](../../deploy/OPERATIONS.md). The supported layout shares Identity's owning database between its HTTP host
and worker and gives Notification its own PostgreSQL deployment.

The foundation uses the existing PostgreSQL outbox, MassTransit **8.5.8**,
RabbitMQ **4.3.6**, separate Identity/Notification worker executables, Notification's
durable inbox and email jobs, Resend, and local Mailpit. See
[architecture decision](../decisions/0003-durable-notification-delivery.md).

Registration stages an encrypted verification email in Identity’s outbox. ID-001 has passed its revised acceptance checks; ID-002 is separate work.
Verification templates point to a future frontend confirmation page; that page
and the confirmation endpoint are separate work.

## Ownership and transaction boundaries

Identity's application abstraction `IVerificationEmailOutbox` stages an encrypted
request in the current Identity transaction. It rejects calls outside that
transaction. Registration stages user, credential, token digest and outbox together; `TransactionPipelineBehavior -> ExecuteInTransactionAsync` saves and commits once.

Identity.Worker claims committed records with SKIP LOCKED, leases and fencing,
publishes with a stable MessageId and mandatory routing, then marks Published
after publisher confirmation. Network I/O occurs outside the database transaction.
Missing durable Notification receipts trigger re-publication after five minutes.

Notification.Worker receives `EmailDeliveryRequestedV1` and sends the custom
`AcceptEmailDeliveryCommand`. One Notification transaction saves the inbox, job
and an `EmailDeliveryRecordedV1` outbox receipt before the consumer acknowledges.
Duplicate events with the same hash produce no new job and reissue a receipt.
Conflicting payloads under an existing ID fail without overwriting the job.

A second worker loop freezes and encrypts rendered provider JSON, persists an
attempt before external I/O, then records acceptance, retry, pause or dead letter.
SMTP/HTTP acceptance is distinct from actual delivery. Resend's signed callbacks
are durably recorded through CQRS before returning 204 and processed asynchronously.
Unmatched receipts are retained for later matching.

No module reads another module's database. Notification stores operational data
under `notification`; technical records use `reliability` in each owning database.
The existing shared helper also maps its IdempotencyRecords table; email transport
uses OutboxMessages/InboxMessages rather than MassTransit's EF outbox.

## Start the local module deployment

Run from the repository root:

```powershell
./scripts/devflow.ps1 init
./scripts/devflow.ps1 up
./scripts/devflow.ps1 status
```

Linux uses `bash ./scripts/devflow.sh init` followed by `up`. The entry point
prepares broker permissions/topology, stages restricted credentials, migrates
each module database, and starts its HTTP host and worker. Local Mailpit is enabled
and available on port 18025 by default. Ports and execution modes are described
in the [deployment guide](../../deploy/README.md).

Identity and Notification use independent PostgreSQL containers/volumes; Identity's
HTTP host and worker use the same database. This layout does not attach or merge
old learning-stack data. Follow [legacy data transition](../../deploy/OPERATIONS.md#legacy-data-transition)
when retaining data from those stacks.

## Mailpit and real email

Local deployment defaults to `DEVFLOW_EMAIL_MODE=Mailpit`; the server template
defaults to `Disabled`. Edit `deploy/env/local.env` and rerun
`./scripts/devflow.ps1 up -Module notification` after changing the mode. Mailpit's
UI defaults to <http://localhost:18025>. Registration is not connected to delivery;
smoke tests use synthetic encrypted requests.

Resend requires an owned, verified sender domain. Before enabling Resend:
configure SPF/DKIM and DMARC, disable click/open tracking, disable paid overages,
set a fixed verified sender/support address and allowlisted HTTPS frontend origin,
and supply non-empty `resend-api.secret` and `resend-webhook.secret` under the ignored
Notification source keys directory. Set mode Resend and domain-verified=true only after DNS verification.
Restart the worker after credential/configuration rotation. Drain or expire existing
jobs before changing provider or sender: frozen requests are intentionally immutable.

The free-phase defaults cap submission reservations at 100/day and 3000/month
in shared database state. Each retry reserves again, including across UTC day/month
boundaries. Reservations are conservative and are not refunded.
Quotas apply across replicas; provider-side limits remain authoritative.
Mailpit requires explicit development mode and must not be enabled in production.

## Encryption and confirmation link

Identity has only Notification's RSA public key. Notification alone holds the
private key. Each request uses a fresh AES-256-GCM key and nonce, wrapped with
RSA-OAEP-SHA256 (minimum RSA 3072). Associated data binds ID, producer, purpose,
template, schema version and timestamps. Ciphertext, never plaintext, crosses
outbox, broker, inbox and dead-letter paths. Logging excludes raw bodies and
transport exception dumps.

Immutable key IDs prevent silent key replacement. Generate a new key ID, distribute
its public key to producers, switch the Notification active key, and retain old
private keys through the outstanding delivery window. Deleting keys before expiry
makes affected jobs unrecoverable. The stable recipient-HMAC key is separate from
content encryption keys and must not be rotated casually.

The versioned English HTML/plain-text verification template includes an escaped
name, accessible button, fallback link, expiry, and ignore-if-unrequested text.
The link is `https://<frontend-origin>/verify-email#token=<opaque-token>`.
Its future page must clear the fragment from history, keep the token in memory,
use no analytics, set no-store/no-referrer/CSP, and require explicit confirmation
before POSTing the token body to ID-002. GET/link scans never activate accounts.

Rendered content is encrypted while retryable and cleared on acceptance/expiry.
Identity retains encrypted recovery events through their lifetime and clears
expired material during maintenance. Sanitized delivery metadata is retained for
30 days. Operator audit history, suppression hashes and deduplication tombstones
remain until a separate approved retention process removes them.

## Failure handling and operator commands

Broker retries are short (1/5/15/30 seconds); poison messages go to _error,
unknown types to _skipped. Provider retries are durable database schedules with
jitter, 15-second initial delay, one-hour cap and at most 24 submission attempts.
Retry-After is respected. Five consecutive failures open a one-minute circuit.
Invalid credentials/domain configuration or missing key files pause sending
until operator intervention. Invalid content/ciphertext dead-letters that job.

Provider requests use `devflow/email/{MessageId}`. Retries reuse exact frozen
request bytes within Resend's 24-hour deduplication window. Stop before the earlier
of expiry minus five minutes and first-attempt plus 23 hours. After that, uncertain
acceptance is retained as AcceptanceUnknown, never blindly resubmitted.

Use authenticated host/orchestrator access; these are not public HTTP operations.
The examples use default container names; use `status` to locate names for a custom
`DEVFLOW_STACK`:

```powershell
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops inspect
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops pause operator-name "Incident reference"
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops resume operator-name "Credentials repaired"
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops redrive operator-name "Failure repaired" MESSAGE_GUID

# Only after confirming acceptance in the provider dashboard/API; never guesses or resends.
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops reconcile-accepted operator-name "Provider evidence incident reference" MESSAGE_GUID PROVIDER_ID
docker exec devflow-local-notification-worker-1 dotnet DevFlow.Notification.Worker.dll ops replay-errors operator-name "Consumer repaired" 10
docker exec devflow-local-identity-worker-1 dotnet DevFlow.Identity.Worker.dll ops inspect
docker exec devflow-local-identity-worker-1 dotnet DevFlow.Identity.Worker.dll ops pause-publication operator-name "Incident reference"
docker exec devflow-local-identity-worker-1 dotnet DevFlow.Identity.Worker.dll ops resume-publication operator-name "Broker repaired"
```

Repairs preserve message identity. Expired/suppressed/accepted/AcceptanceUnknown jobs
are not blindly redriven. A replacement verification token belongs to future Identity
business flows. Replays can repeat processing if interrupted; inbox and provider
idempotency are required. Host login/orchestrator audit authenticates the operator;
the supplied name/reason is supporting audit metadata, not authentication.

## Metrics, backups and production gates

Workers/API reuse DevFlow.Observability. Enable the delivery observability override
after starting the existing observability stack; it exports named Messaging and
Notification meters without recipient or payload dimensions. Load
deploy/delivery/alerts.yaml in Prometheus rule_files. Collector privacy filters
remain unchanged. Broker depth/leader/disk monitoring is a separate RabbitMQ
monitoring concern and must also be configured before production.

Alert on pending age >5 minutes, missing receipts >5 minutes, any dead letters,
worker heartbeat >60 seconds, quota pressure, unmatched callbacks, disk alarms,
and backup/WAL failures. Readiness checks database connectivity only, not provider
deliverability, DNS, schema completeness, or disaster-recovery readiness.

Production additionally requires:

- RabbitMQ quorum replicas on three separate failure domains, authenticated TLS
  with normal certificate validation, and tested safe dead lettering.
- PostgreSQL durable storage with fsync/full_page_writes/synchronous_commit enabled.
- Encrypted off-host base backups and continuous WAL archival with verified recovery.
- Separate recoverable encryption keys, secret rotation and tested access controls.
- A verified sender domain, Resend account/MFA and signed HTTPS callback exposure.
- Restore drills demonstrating a chosen RPO/RTO; a suggested starting target is
  RPO <=5 minutes and RTO <=4 hours, not an untested guarantee.

Those cloud/DNS/backup destinations are not supplied by this repository. A single
VPS and its local volumes cannot survive total host loss. Exactly-once inbox
delivery and recovery from loss of every database/key copy cannot be guaranteed.
The application-level configuration reference is
`deploy/env/.delivery.production.env.example`; it is not a production deployment.

## Focused verification

See [verification coverage and production acceptance](VERIFICATION.md) for the
distinction between automated failure tests and remaining deployment drills.

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-EmailDelivery.ps1
dotnet test tests/DevFlow.AspNetCore.HttpClient.Tests/DevFlow.AspNetCore.HttpClient.Tests.csproj
dotnet test tests/DevFlow.Identity.Tests/DevFlow.Identity.Tests.csproj
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Architecture.ps1
```

The delivery harness creates disposable PostgreSQL, RabbitMQ and Mailpit containers,
creates only generated test databases, runs tests, and removes its containers.
It tests actual pg_dump/pg_restore and broker restart/error replay as well as
atomic persistence, leases, deduplication, provider timeouts, crypto, signatures,
and local SMTP/template delivery. Docker can reassign random published ports on
restart; the harness rediscovers the broker's port.

Direct dotnet test runs explicitly skip unavailable integration dependencies.
No test sends real email or modifies the existing Identity database. Actual Resend
sending, external DNS, production TLS/three-node failover, off-host WAL archival
and timed production restoration remain deployment acceptance work.

## Stop locally

```powershell
./scripts/devflow.ps1 down
```

Do not add -v unless deliberately discarding the local delivery databases/queues.
