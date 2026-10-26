# Deployment operations

Use the same environment file, `DEVFLOW_STACK`, module selection, and execution
mode used to start the deployment. Commands return nonzero on failure. Source
secrets and named volumes survive ordinary `down`.

## Redeploy a module

Build and test a release on the build machine, publish immutable images, and update
the selected module's image references in the server environment file. Then run:

```bash
bash ./scripts/devflow.sh check --environment server --module identity
bash ./scripts/devflow.sh up --environment server --module identity
bash ./scripts/devflow.sh status --environment server --module identity
bash ./scripts/devflow.sh logs --environment server --module identity
```

PowerShell uses corresponding `-Environment`, `-Module`, and `-Config` parameters.
Each `up` runs a new migration process and reapplies grants before replacing the
selected runtimes. It does not rely on a previously exited migration container.
Migration failure blocks that module's rollout; inspect the job log and repair
the cause. The other module is not migrated.

Plan compatible schema changes before rollout. An older image can be restored
only if it supports the current schema and contracts. Deployment does not run
down-migrations, rewind databases, or recreate keys automatically.

## Stop and restart

```powershell
./scripts/devflow.ps1 down -Module notification
./scripts/devflow.ps1 up -Module notification
```

Selecting one module leaves platform and the other module running. Default `all`
selection also stops platform and telemetry. Never add `--volumes`/`-v` as a
troubleshooting step. Queue data and pending deliveries need the same care as
business rows.

## Diagnose a failed startup

| Failure | Action |
| --- | --- |
| Placeholder setting | Correct the named nonsecret field and rerun `check`. Server examples require real image/broker values. |
| Missing/unreadable secret | Provision or restore the named file without printing it. Rerun staging through `up`. |
| Port conflict | Change the environment setting or intentionally stop the conflicting application. Setup never kills unrelated containers. |
| PostgreSQL job fails | Inspect bootstrap, migration, and grant jobs. Check the owning database/credential; do not substitute the other module's database. |
| Broker setup/connection fails | Verify broker, credentials, vhost, permissions, TLS trust, and topology. Do not bypass Production messaging validation. |
| Worker heartbeat expires | Inspect storage/broker availability and logs. A running process can fail its successful-scan check. |
| HTTP ready but registration fails | Check Redis and the application logs. Database readiness does not cover Redis. |
| Local mailbox is empty | Registration is not connected to delivery. Use synthetic encrypted delivery tests and check SMTP connectivity. |

Use `status` and `logs`; do not dump resolved Docker configuration, environment
values, user-secrets lists, or provider payloads into shared logs.

### Recover from a missing Docker build snapshot

An error such as `failed to stat parent` followed by a missing
`desktop-containerd/.../snapshots/.../fs` path can indicate that Docker Desktop's
shared builder cache references a snapshot that is no longer present. A separate
BuildKit builder gives this project a fresh cache without deleting existing
images, volumes, containers, or the shared builder's cache. It does not repair the
underlying Docker Desktop cache for other projects.

On the affected computer, run the following from a terminal with Docker running.
The `default-load` option requires [Buildx 0.14 or newer](https://docs.docker.com/build/builders/drivers/#load-by-default).
Docker Desktop normally includes Buildx; check with `docker buildx version`.

```text
docker buildx create --name devflow-local-build --driver docker-container --driver-opt default-load=true
```

This creates a builder without changing Docker's globally selected builder. If
the name already exists, inspect it with `docker buildx inspect devflow-local-build`
and use it only if it is the intended builder for the active Docker context;
otherwise choose another unique name. Do not delete or recreate someone else's
builder. The first build with this builder downloads layers and packages again and
requires additional disk space.

Add this nonsecret line to your existing ignored `deploy/env/local.env`, using the
chosen builder name. Leave all other settings and secret files intact:

```dotenv
BUILDX_BUILDER=devflow-local-build
```

Then run `check` and `up` through `scripts/devflow.ps1` or `scripts/devflow.sh` as
usual. Their env-file loader passes the selection to both the setup-image build
and Compose builds. Use the same `-Config`/`--config` file if your deployment uses
a different configuration path. Verify the output names the chosen builder and
wait for application readiness. The value in that file overrides an inherited
`BUILDX_BUILDER` for these script invocations. Remove the line to return to the
Docker-selected builder after its cache is repaired.

For the Docker behavior behind this selection, see [Docker's builder documentation](https://docs.docker.com/build/builders/).
If a fresh builder also fails, inspect the first error and Docker Desktop's
diagnostics with your team before changing shared Docker storage.

## Credential and encryption-key rotation

`init` creates missing local credentials; it does not rotate existing values.
Replacing a password file alone does not update a retained PostgreSQL database or
broker account. Use a reviewed procedure that changes the external credential,
source file, staged files, and affected runtimes together. Keep an authenticated
recovery path throughout rotation.

Database administration, migrations, API/runtime access, and broker access use
separate credentials. Identity's worker receives reliability permissions only.
Runtime roles cannot create/alter schemas. Reapplying grants after migrations
includes new objects and preserves append-only restrictions.

Encryption key IDs are immutable. Add a new pair under a new ID, distribute only
the public key to producers, deliberately switch the active key, and retain old
private keys until pending jobs no longer need them. Preserve the recipient-HMAC
secret separately; changing it changes suppression identity. Restart affected
runtimes after changing startup-loaded secrets.

## Backups and restore

Back up each module's PostgreSQL database independently to an encrypted off-host
destination. Include migration history and reliability records. Recover encryption
private keys and recipient-HMAC material through a separately protected backup
path. A database backup alone cannot recover encrypted pending deliveries.

Choose explicit RPO/RTO targets and restore into an isolated project with new
names/ports. Verify migrations, credentials, pending work, deduplication, and
synthetic receipt/delivery before accepting the restore. Match RabbitMQ durability
and PostgreSQL WAL/base-backup practices to that recovery target. Named volumes
are persistence, not off-host backup or high availability.

## Legacy data transition

The replacement uses fresh project names. It does not attach, delete, merge, or
rename old Identity/delivery volumes. Earlier stacks may hold different Identity
databases (`DevFlowIdentity` and `DevFlowIdentityDelivery`). Do not point a worker
at either arbitrarily or merge their tables directly.

1. Inventory databases, versions, migrations, queues, key IDs, and credential
   locations without exporting secret values into logs or tickets.
2. Stop writes/publishers for the agreed export/import window; back up each owning
   database and its required keys separately.
3. Select the authoritative Identity database. Review outstanding events in the
   old learning database and broker before retiring them.
4. Restore into the corresponding new module database with new runtime roles;
   apply reviewed pending migrations and runtime grants.
5. Verify registration and encrypted delivery/receipts in isolation. Preserve
   original message IDs when replaying pending work.
6. Switch endpoints after verification. Retain original backups/volumes until the
   recovery window is closed deliberately.

No automatic data migration between old stacks and the replacement is provided.

## Real email

Local operation uses Mailpit. Server operation defaults to Disabled: jobs can be
accepted durably but no email is sent. Before Resend, supply an owned verified
sender domain, SPF/DKIM/DMARC, nonempty API/webhook secrets, and a trusted HTTPS
frontend origin. Apply the account/tracking/overage requirements in the
[Notification runbook](../docs/notification/README.md).

Signed callback exposure requires external TLS ingress. A private HTTP listener
is not a public callback deployment. Verify authentication, quotas, idempotency,
provider acceptance, and delivery outcomes with controlled accounts. Changing
provider/sender does not modify frozen requests; drain or expire pending work
according to Notification's recovery rules.

## Publish release images

From a tested checkout on the build machine, authenticate to your registry and
publish all seven images. For example (replace registry/release; choose the server's
CPU platform):

```bash
registry=registry.company.example/devflow
release=v1.0.0
docker buildx build --platform linux/amd64 -f Dockerfile.deploy -t "$registry/setup:$release" --push .
for target in identity-api identity-worker identity-migration notification-api notification-worker notification-migration; do
  docker buildx build --platform linux/amd64 -f Dockerfile.modules --target "$target" -t "$registry/$target:$release" --push .
done
```

Record resulting digests and promote those exact artifacts to the server env file.
Do not rebuild independently on the deployment host. Refresh pinned base digests
through a reviewed update and rerun onboarding acceptance before promoting images.

## Acceptance evidence

The onboarding interface is the deployment test surface: fresh/repeated startup,
retained-data restart, each module alone, non-default ports, Docker/IDE modes, and
intentional dependency/configuration failures. Verify runtime roles cannot perform
DDL or access another module's database. Use synthetic encrypted delivery, receipt,
and deduplication tests rather than adding a test endpoint.

Run the reproducible Windows acceptance harness with a dedicated env file copied
from `local.env.example`. Set `DEVFLOW_STACK=devflow-verify-<unique-id>`, a matching
new secret directory, and unused ports. It generates missing local secrets, leaves
the verification deployment running, and retains volumes on shutdown:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Deployment.ps1 -Config deploy/env/verification.env
./scripts/devflow.ps1 down -Config deploy/env/verification.env
```

The harness restores previous IDE user-secrets after testing. Failure injection
changes only credentials belonging to that explicitly named verification stack
and restores them in `finally` blocks. Run it without other operations against
the same verification deployment. To exercise just the deployed .NET checks:

```powershell
$env:DEVFLOW_DEPLOYMENT_TEST_CONFIG = (Resolve-Path deploy/env/verification.env).Path
dotnet test tests/DevFlow.Notification.Tests --filter FullyQualifiedName~DeploymentSmokeTests
```

Verified on 2026-10-26 with Docker 29.5.2 / Compose 5.1.4:

- Windows full Docker: fresh/repeated startup, retained-volume restart, independent
  Identity and Notification redeploys, and custom ports; IDE configuration and a real host API ready.
- Broker bootstrap and migration failures blocked runtime replacement and recovered
  with the original credentials; duplicate settings and occupied ports failed early.
- Actual encrypted worker delivery reached Mailpit, receipts returned to Identity,
  and replay produced one delivery/attempt/message. Runtime roles could not create
  schemas, access another module's database, or let Identity's worker read users.
- Bash full Docker ran on a real Linux filesystem inside a Docker CLI runner:
  fresh startup, repeat Identity startup, custom ports, and world-readable-secret
  rejection passed. Native Linux IDE execution was not exercised.
- Full shutdown on both entry points removed optional Mailpit containers and
  retained volumes and credentials. The disposable verification deployments were
  stopped after acceptance; unrelated existing deployments were left running.
- The opt-in telemetry stack started; Grafana, Prometheus, Loki, Tempo, and collector
  metrics endpoints responded. Server models passed image-only/private-listener
  assertions and server preflight using verification configuration.
- Solution tests: 72 passed, 18 dependency-specific tests skipped. Architecture
  checks passed for all 24 projects. NuGet vulnerability metadata was unavailable
  from this machine (NU1900); the tests themselves completed successfully.

Docker Desktop's existing shared build cache reported a missing snapshot during
verification. An isolated task-owned `docker-container` Buildx builder completed
the builds; no shared cache was pruned. If this occurs locally, select an isolated
builder through `BUILDX_BUILDER` rather than deleting teammates' Docker data.

External TLS broker/ingress, real Resend sending, backup restoration, and total
host-loss recovery were not exercised. Existing .NET suites and
`scripts/Test-EmailDelivery.ps1` remain useful additional checks, but do not prove
those deployment prerequisites.

Gateway implementation and verification on 2026-10-28 are recorded in the
[gateway operations guide](../docs/gateway/README.md#verification-evidence), including
production-mode forwarding, backend outage isolation, Windows/Linux lifecycle,
durable registration delivery, and live telemetry privacy checks. Use a distinct
HTTP subnet and gateway IP for each concurrent disposable verification stack.
