# DevFlow

.NET 10 codebase with independently deployed Identity and Notification modules.
Each module owns its PostgreSQL database and runs separate HTTP, worker, and
migration containers. RabbitMQ is shared infrastructure. The gateway is future
work; registration does not enqueue verification emails yet.

## Start locally

New to the project or Docker? Follow the [step-by-step local setup guide](docs/LOCAL_DOCKER_SETUP.md)
for prerequisites, environment configuration, verification, and troubleshooting.

Install Docker Engine/Desktop with Compose **2.24.4+**. Use Linux containers.
The entry points resolve repository paths themselves.

Windows PowerShell:

```powershell
./scripts/devflow.ps1 init
./scripts/devflow.ps1 check
./scripts/devflow.ps1 up
./scripts/devflow.ps1 status
```

Linux Bash:

```bash
bash ./scripts/devflow.sh init
bash ./scripts/devflow.sh check
bash ./scripts/devflow.sh up
bash ./scripts/devflow.sh status
```

`init` creates ignored local settings and missing secrets without replacing
existing values. `up` builds local images, prepares RabbitMQ, runs each module's
migrations and grants, and waits for selected runtimes. Mailpit is enabled locally;
telemetry is opt-in. First startup downloads images and restores .NET packages.

| Local endpoint | Default |
| --- | --- |
| Identity readiness | <http://localhost:8081/health/ready> |
| Notification readiness | <http://localhost:8082/health/ready> |
| Mailpit inbox | <http://localhost:18025> |

Ports are configurable in `deploy/env/local.env`. Published local ports bind to
loopback. New projects are `devflow-local-platform`, `devflow-local-identity`, and
`devflow-local-notification`; legacy projects and volumes are not changed.

## Select a workflow

```powershell
# Operate one module without restarting the other.
./scripts/devflow.ps1 up -Module identity
./scripts/devflow.ps1 logs -Module notification

# Run infrastructure and migrations, then launch .NET hosts in the IDE.
./scripts/devflow.ps1 up -Mode host

# Add telemetry to the selected deployment.
./scripts/devflow.ps1 up -Telemetry

# Stop the deployment; keep its data and source secrets.
./scripts/devflow.ps1 down
```

Bash uses `--module`, `--mode`, `--telemetry`, `--environment`, and `--config`.
Stop selected Docker-hosted applications before switching to IDE mode. Host mode
requires the .NET 10 SDK from [global.json](global.json), configures separate
user-secrets stores, and uses **Local** launch profiles. See
[developer workflows](deploy/README.md#developer-workflows).

## Build and test

```powershell
dotnet restore DevFlow.slnx
dotnet build DevFlow.slnx -m:1
dotnet test DevFlow.slnx -m:1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Architecture.ps1
```

Tests requiring external services skip when their test configuration is absent.
Use disposable resources for integration tests. The existing Tasks host is outside
this supported deployment; its source startup-registration blocker is not repaired
by environment variables.

## Deployment guidance

- [Setup, lifecycle, and Linux deployment](deploy/README.md)
- [Environment variables and secret files](deploy/ENVIRONMENT.md)
- [Redeployment, recovery, and legacy-data transition](deploy/OPERATIONS.md)
- [Architecture and module ownership](ARCHITECTURE.md)
- [Deployment decision](docs/decisions/0004-module-oriented-docker-deployment.md)
- [Identity migrations](docs/identity/MIGRATIONS.md) and
  [Notification operations](docs/notification/README.md)

The Linux template uses prebuilt images and private HTTP listeners. It requires
operator-provided TLS RabbitMQ and TLS ingress before public use. It is a
single-host application deployment, not a high-availability guarantee.
