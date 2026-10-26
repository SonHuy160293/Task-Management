# Durable Notification delivery and executable workers

Status: accepted for implementation by the user, 2026-10-26.

Identity owns accounts/challenges and publishes encrypted delivery requests from its
existing transaction-owned outbox. Notification owns inbox acceptance, templates,
provider calls, durable retries, outcomes and suppression in its own database.
MassTransit 8.5.8/RabbitMQ provide transport, not an alternative application mediator
or EF outbox. Packages remain pinned to v8; a license/security review is required
before changing major versions.

Worker projects are now executable composition roots allowed to reference their own
Application and Infrastructure, Contracts and BuildingBlocks. APIs must not reference
worker executables. Cross-module Domain/Infrastructure dependencies remain forbidden.

The repository's reliability OutboxMessage/InboxMessage models are reused. Shared
relay/publisher code lives in DevFlow.Messaging. Module-owned operations still use
the custom ISender and UnitOfWork transaction behavior. Background delivery steps
have their own short transactions and never hold a transaction across broker/HTTP I/O.

Notification records a job and receipt before acknowledging RabbitMQ. Identity can
republish confirmed requests when a receipt is missing. Notification deduplicates
by message ID and immutable payload hash. This is at-least-once processing.
Email acceptance outside the database cannot be exactly-once: Resend idempotency
and a bounded retry horizon reduce duplicates, while unknown acceptance is retained
for investigation.

The local deployment is a separate learning stack with its own PostgreSQL volume,
two databases, independent runtime/migration credentials, one RabbitMQ quorum member,
and optional Mailpit. It neither modifies the running Identity database nor wires
registration or email confirmation. Production requires TLS, multiple failure
domains, off-host backups and separate key recovery. New migrations are reviewed
source changes and are applied only by explicit migration hosts.

Encrypted notification copies are the deliberate exception to hash-only verification
storage: Identity's token table still contains only the digest. Only Notification
holds the RSA private key; brokers, outboxes, inboxes and errors carry ciphertext.
