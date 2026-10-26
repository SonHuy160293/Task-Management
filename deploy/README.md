# Module deployment

For first-time local setup, follow the [intern Docker walkthrough](../docs/LOCAL_DOCKER_SETUP.md).
This deployment reference covers lifecycle behavior and advanced workflows.

The supported deployment has platform and gateway projects and independently operated
Identity and Notification projects. Each module owns its database, credentials,
migrations, runtime containers, and volumes. Its HTTP host and worker use the same
database. Superseded root-level Compose files, mixed environment examples, and
their setup scripts have been removed. Existing legacy volumes and secrets are
untouched; see the [legacy transition procedure](OPERATIONS.md#legacy-data-transition).

## Prerequisites

- Docker Engine/Desktop with Linux containers and Compose 2.24.4 or later.
- Windows PowerShell or Linux Bash and standard GNU tools.
- Internet access for image downloads and package restore during local builds.
- For IDE mode, a .NET 10 SDK compatible with `global.json`.

Use the entry points in `scripts/`; they own cross-project startup ordering.
Direct Compose commands are for diagnosis or reviewed recovery.

## Onboarding interface

| Action | Behavior |
| --- | --- |
| `init` | Create a missing environment file; locally generate missing secrets and keys. Existing values are retained. Server init creates the template only. |
| `check` | Validate prerequisites, settings, Compose models, selected secret files, and published-port conflicts. Does not start the deployment. |
| `up` | Prepare prerequisites, explicitly run migrations/grants, start selected runtimes, and wait for health checks. |
| `down` | Stop selected modules. With `all`, also stop platform and telemetry. Retain named volumes and source secrets. |
| `status` | Show platform, selected modules, and telemetry containers, including completed jobs. |
| `logs` | Show the latest 100 log lines for platform and selected modules. |

| PowerShell parameter | Bash parameter | Default |
| --- | --- | --- |
| `-Environment local\|server` | `--environment local\|server` | `local` |
| `-Module all\|identity\|notification\|gateway` | `--module all\|identity\|notification\|gateway` | `all` |
| `-Mode docker\|host` | `--mode docker\|host` | `docker` |
| `-Telemetry` | `--telemetry` | Off |
| `-Config path` | `--config path` | `deploy/env/<environment>.env` |

Relative config paths resolve against the repository. Use the same config and
options for lifecycle commands. Server host mode is rejected.

```powershell
./scripts/devflow.ps1 init
./scripts/devflow.ps1 check
./scripts/devflow.ps1 up
./scripts/devflow.ps1 up -Module notification
./scripts/devflow.ps1 status
```

```bash
bash ./scripts/devflow.sh init
bash ./scripts/devflow.sh check
bash ./scripts/devflow.sh up
bash ./scripts/devflow.sh up --module notification
bash ./scripts/devflow.sh status
```

## Startup implementation

Local startup validates configuration, prepares RabbitMQ permissions and durable
topology, then handles each selected module: stage restricted secrets, wait for
PostgreSQL, create missing roles/database, run migrations, reapply runtime grants,
and start the HTTP host and worker. Identity also starts its own Redis. A failed
step returns nonzero and prevents later steps for that module; it does not undo
already completed infrastructure changes.

Module jobs do not wait on the other module's database. Repeated `up` is the normal
redeployment path: it reruns migrations even if an older migration job succeeded.
APIs do not perform DDL and no business data is seeded.

Project names derive from `DEVFLOW_STACK`: `<stack>-platform`, `<stack>-identity`,
`<stack>-notification`, and optional `<stack>-telemetry`. Platform owns the shared
messaging and HTTP networks. Default Docker startup adds gateway after the modules.
Gateway-only startup prepares shared networking without brokers, databases, or workers.
Module-specific operation leaves gateway unchanged. Each module owns its database network and named volumes.

## Developer workflows

Default `docker` mode runs both complete modules and local Mailpit. Select one
with `-Module`/`--module` to operate it independently. Shared prerequisites may be
prepared again; the other module is not redeployed.

For IDE execution, stop the selected Docker-hosted applications first:

```powershell
./scripts/devflow.ps1 down -Module identity
./scripts/devflow.ps1 up -Module identity -Mode host
```

```bash
bash ./scripts/devflow.sh down --module identity
bash ./scripts/devflow.sh up --module identity --mode host
```

Host mode starts infrastructure and migrations, then populates the selected
executables' user-secrets stores from the same local settings and source secrets.
Launch the desired HTTP host and worker with **Local** profiles in the
IDE, or `dotnet run --launch-profile Local --project <project>`.
HTTP and background work remain separate processes.

Stores are `devflow-gateway`, `devflow-identity-api`, `devflow-identity-worker`,
`devflow-identity-migrationrunner`, `devflow-notification-api`,
`devflow-notification-worker`, and `devflow-notification-migrationrunner`.
Refresh host mode after changing ports/settings. Do not independently rotate the
generated credentials in a user-secrets store. User secrets are developer storage,
not an encrypted server secret manager.

### Local endpoints

| Setting | Default |
| --- | --- |
| Gateway HTTP / frontend API base | `127.0.0.1:8080` |
| Identity HTTP | `127.0.0.1:8081` |
| Notification HTTP | `127.0.0.1:8082` |
| Identity PostgreSQL | `127.0.0.1:15431` |
| Notification PostgreSQL | `127.0.0.1:15432` |
| Identity Redis | `127.0.0.1:16379` |
| RabbitMQ AMQP / management | `127.0.0.1:5672` / `127.0.0.1:15672` |
| Mailpit SMTP / inbox | `127.0.0.1:11025` / `127.0.0.1:18025` |
| Optional Grafana | `127.0.0.1:13000` |

Change ports in the environment file and rerun `check`/`up`. Host mode regenerates
host endpoints. Container traffic uses internal DNS/ports; management setup does
not assume the published host management port.

## Telemetry

Use `up -Telemetry` or `up --telemetry` to add the collector and storage stack.
Repeat the option on later operations that should retain telemetry configuration.
Telemetry is not an API readiness prerequisite. See [settings](ENVIRONMENT.md)
and [telemetry implementation notes](observability/README.md).

## Resource budgets

Compose applies memory ceilings and bounded JSON logs; these are guardrails, not
measured production capacity targets. Size databases, broker storage, and backup
space for actual retention and pending work.

| Container | Memory ceiling |
| --- | --- |
| Each API, worker, or migration job | 512 MiB |
| Each PostgreSQL | 768 MiB |
| Local RabbitMQ | 1024 MiB |
| Identity Redis | 256 MiB |
| Local Mailpit | 128 MiB |
| Each optional telemetry process | 512 MiB |

Both local modules plus the gateway have about 5.4 GiB of steady-state container ceilings; a
migration adds 512 MiB and telemetry adds 2.5 GiB. Allow additional memory for
Docker, the OS, and source builds. Start with at least 8 GiB available to Docker
for the basic workflow and 12 GiB with telemetry/builds, then observe usage.
Builds benefit from four or more CPU cores. Logs rotate at 10 MiB × three files per
container; database/broker volumes need separate disk monitoring. Runtimes receive
45 seconds for graceful shutdown; deployments are not zero-downtime rollouts.

## Linux host template

```bash
bash ./scripts/devflow.sh init --environment server
# Edit deploy/env/server.env and provision its required secret files.
bash ./scripts/devflow.sh check --environment server
bash ./scripts/devflow.sh up --environment server
```

Select an independent deployment with `--module identity` or `--module notification`.
Replace image/broker placeholders in `server.env.example`. Supply immutable release
tags or digests for setup and application images. Build/push on the build machine;
server startup pulls images and never builds application source.

Production validation remains enabled. Supply an operator-managed RabbitMQ cluster
with trusted TLS, per-module credentials, vhost, permissions, and durable topology.
The template does not deploy the local development broker or provision a production
cluster. Configure that external broker before `up`.

Only gateway publishes a loopback HTTP listener on servers; backend APIs publish
no host ports. Provide external TLS ingress targeting only gateway and configure
the observed trusted ingress peer IP and public hostname. Identity retains HTTPS
protection through trusted forwarding; internal health checks work over HTTP.
See [gateway operations](../docs/gateway/README.md) for routes, IDE execution,
networking, CORS, configuration reloads, rollout, and rollback. Keep Resend disabled until the
[email acceptance prerequisites](OPERATIONS.md#real-email) are met.

This is a single-host application template. External TLS, broker quorum across
failure domains, off-host backup, alert routing, and recovery remain operator
responsibilities. Local volumes do not survive total host loss.

## Operations and validation

Read [environment/secret rules](ENVIRONMENT.md) before configuration changes and
[operations](OPERATIONS.md) before migrations, rotation, import, or recovery.
Check `/health/ready` on both HTTP hosts; worker health is a bounded successful-scan
heartbeat. Neither proves external delivery or every business flow. Use synthetic
integration tests for those outcomes.

Validation evidence is recorded in [operations](OPERATIONS.md#acceptance-evidence). A successful Compose parse does not
prove production dependencies or runtime behavior.

Registration email requires the selected Notification public key in Identity�s own `identity/notification-keys/<key-id>.public.pem` secret directory. Local `init` copies available public keys without overwriting existing IDs; run it once after upgrading. For servers, provision this public copy independently before starting Identity or rotating `DEVFLOW_EMAIL_ACTIVE_KEY_ID`. The Identity API mounts only the selected public key and loads it on startup. See [environment reference](ENVIRONMENT.md).
