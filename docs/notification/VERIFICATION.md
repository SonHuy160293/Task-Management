# Delivery foundation verification

The automated harness uses disposable PostgreSQL 18, RabbitMQ 4.3.6 and Mailpit,
not the existing Identity database. Run `scripts/Test-EmailDelivery.ps1` to repeat it.

## Coverage

| Failure or invariant | Automated evidence |
| --- | --- |
| Business/outbox atomicity | Real PostgreSQL commit and exception rollback |
| Broker unavailable | Pause the disposable broker; relay preserves a retryable committed row |
| Abandoned claims and concurrent publishers | Competing PostgreSQL claims, expired lease recovery, stale owner fencing |
| Confirmation followed by missing receipt | Published state reconciles to the same message ID with recovery audit |
| Missing binding | Mandatory RabbitMQ publication fails rather than silently succeeding |
| Duplicate delivery / consumer failure after commit | Real broker retry and concurrent CQRS calls create one inbox/job |
| Conflicting content under an existing ID | Transaction fails without overwriting the accepted job |
| Broker error durability | Poison message survives actual broker restart and is replayed with original identity |
| Unknown provider acceptance | Simulated timeout reuses identical frozen content and identity on retry |
| UTC quota rollover | Retried submissions reserve the new day's allowance; subsequent work is blocked at the limit |
| Expiry / key retirement | Expired work is never sent; duplicate receipt does not need the retired private key |
| Payload confidentiality | AES-GCM tampering/associated-data tests and redacted diagnostics |
| Callback authentication | Svix signature, modified body and replay timestamp checks; HTTP forgery/size/duplicate cases |
| Confirmation template | Real SMTP capture contains HTML button and plain-text fragment link |
| Restore recovery | Actual pg_dump/pg_restore leaves original outbox work recoverable |
| Acceptance reconciliation | Requires a prior attempt and no live lease; records acceptance without another submission |

The test suite simulates crash boundaries using abandoned leases, provider timeouts
and consumer exceptions. It does **not** kill real production processes or establish
exactly-once external email delivery. The backup test is a local logical restore,
not evidence of off-host WAL recovery, measured production RPO/RTO or key-backup recovery.

## Other targeted checks

- Both executable workers and the Notification API/migration container images build.
- Identity's receipt-only CQRS composition validates in Development without the
  API registration/password/Redis dependencies.
- `scripts/Test-Architecture.ps1` validates the executable-worker boundary.
- Shared HTTP-client regression tests exercise the existing integration behavior.
- Identity tests run against an isolated migrated PostgreSQL database for rollback,
  concurrent duplicate registration and the unique email index. Redis-dependent
  checks require their own isolated Redis configuration and may be skipped.
- The local delivery stack uses restricted runtime database/broker credentials;
  its readiness endpoint checks database connectivity. It sends nothing by default.

Builds still report analyzer/style warnings, including an existing StyleCop analyzer
exception for record declarations. No analyzer was disabled to manufacture a pass.

## Deployment acceptance still required

Do not describe this local stack as production disaster recovery or high availability.
Before enabling real delivery, supply and verify:

- Sender-domain DNS, Resend credentials, sender authorization, provider account
  limits/disabled paid overages, signed public HTTPS callbacks and disabled tracking.
- RabbitMQ TLS, three quorum members on independent failure domains, broker metrics,
  disk/capacity alarms and failure drills using the actual production topology.
- Off-host encrypted PostgreSQL base backups/WAL archival, separately recoverable
  encryption keys, and restoration/replay drills meeting the agreed RPO/RTO.
- Collector/Prometheus routing and actual alert delivery; metrics/rule definitions
  alone do not prove the paging path works.
- Production load, deployment interruption and long-running provider-outage drills.
- Live key rotation while unexpired jobs and frozen requests still need old keys.
- End-to-end secret-leak review of the production logging/collector/error paths.

ID-001 enqueueing and the future frontend/ID-002 confirmation remain deliberately
outside this foundation. No real email or registration integration is exercised.
