# Gateway development and operations

Frontend API calls use one gateway base URL. In local Docker that defaults to
`http://localhost:8080`; production uses the HTTPS hostname owned by your ingress.
Tasks has no gateway route or supported deployment yet.

| Public request | Internal request | Deadline | Maximum body |
| --- | --- | --- | --- |
| `POST /identity/api/v1/auth/register` | Identity `/api/v1/auth/register` | 30 seconds | 16 KiB |
| `POST /notification/api/v1/webhooks/email/resend` | Notification `/api/v1/webhooks/email/resend` | 15 seconds | 64 KiB |

Configure the provider callback using the second public URL. Its raw body and
`svix-*` headers reach Notification unchanged, where signature verification runs.
Registration retains its rate limits and durable encrypted email/outbox flow.
The gateway neither authenticates bearer tokens nor stores JWT signing keys.

## Run locally

The existing `init`, `check`, and `up` commands default to all modules plus gateway.
Use a PowerShell invocation policy override if required on your machine:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/devflow.ps1 init
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/devflow.ps1 check
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/devflow.ps1 up
```

```bash
bash scripts/devflow.sh init
bash scripts/devflow.sh check
bash scripts/devflow.sh up
```

`-Module gateway` / `--module gateway` operates only the gateway and prerequisite
shared networking. It does not start databases, RabbitMQ, or workers. Identity and
Notification selections leave the existing gateway unchanged. Stop the gateway
with a gateway selection; full shutdown stops it before shared networking.

Local APIs retain loopback ports 8081 and 8082 for Postman and Identity Swagger.
Browser frontend code must target the gateway. Internal integrations use unique
Docker aliases `http://identity-api:8080` and `http://notification-api:8080`, their
original paths, and the destination's required credentials. Internal callers do
not add the gateway's module prefix. Asynchronous integrations use existing workers
and RabbitMQ directly.

For IDE execution, stop Docker-hosted applications, run `up -Mode host` /
`up --mode host`, then launch the gateway and desired APIs with their **Local**
profiles. Gateway settings use the `devflow-gateway` user-secrets store. Refresh host
settings after changing ports; gateway user secrets contain no authentication keys.

## Deployment settings

| Setting | Default / requirement |
| --- | --- |
| `DEVFLOW_GATEWAY_IMAGE` | `devflow-gateway:local`; immutable release tag/digest on servers |
| `DEVFLOW_GATEWAY_PORT` | `8080`, bound to host loopback |
| `DEVFLOW_GATEWAY_PUBLIC_HOST` | `localhost`; server public API hostname, without scheme/path |
| `DEVFLOW_HTTP_SUBNET` | `172.30.250.0/24`; configurable IPv4 `/8` through `/29`, without Docker network overlap |
| `DEVFLOW_GATEWAY_IP` | `172.30.250.10`; usable address in that subnet, excluding its bridge address |
| `DEVFLOW_INGRESS_PROXY_IP` | Local default `172.30.250.1`; required explicit server ingress peer IPv4 |
| `DEVFLOW_FRONTEND_ORIGIN` | Exact frontend origin; production requires HTTPS, without trailing slash/path |

Use a different subnet/IP pair for concurrently running verification stacks. Stop
the full stack before changing its existing HTTP subnet. Preflight inspects network
definitions without mounting the Docker socket in application/setup containers.

Only gateway HTTP is published on servers. Terminate HTTPS at the external ingress
and forward to the gateway's loopback listener. Permit the public hostname and the
two public paths; the gateway itself also permits its liveness/readiness endpoints.
There is no public Swagger, integration, backend-health, static-file, or management
proxy route.

Ingress must discard client-supplied forwarding headers and set the verified client
IP, original HTTPS scheme, and public host. Configure its peer address as **observed
by the gateway**, accounting for Docker NAT; do not assume it is the ingress host's
public IP. The gateway trusts one hop and overwrites downstream forwarding values.
Backend APIs trust the fixed gateway IP only. This keeps Identity's HTTPS enforcement
and client-IP registration limits correct. Never clear trust restrictions or enable
automatic trust of arbitrary proxies. Host filtering includes loopback for health
checks; a public ingress must preserve the configured public Host.

## Routes and policies

The gateway publishes `proxy.json` beside its executable. It contains standard YARP
`ReverseProxy` routes/clusters. `Gateway:RoutesFile` can select another file. Runtime
configuration, including environment/user-secrets destination overrides, takes
precedence; those overrides require restart to change. A file update reloads routes
without a rebuild. Invalid JSON or invalid routes/clusters log a static rejection
and retain the previous snapshot. Invalid initial configuration exits nonzero.

For operator-managed live updates, mount a **directory**, read-only, containing
the file and point `Gateway__RoutesFile` to it; mount permissions must allow the
non-root runtime to read it. Replace files atomically in that directory. A direct
single-file bind can retain the old inode after atomic replacement. Set
`DOTNET_USE_POLLING_FILE_WATCHER=1` where the mounted filesystem lacks change events.
Default Compose uses the immutable image's configuration; redeploying that image
is the normal release path.

When adding a public capability, add a method/path-specific route, cluster reference,
prefix transformation, explicit positive timeout (maximum 30 seconds), and body
limit (maximum 64 KiB). Add the frontend CORS policy where appropriate. Catch-all
routes are rejected; module management/integration interfaces stay private. Add a
transport test and update this route inventory. Increase gateway ceilings explicitly
before introducing uploads or long-running protocols.

CORS permits configured exact origins, POST, and the configured authorization,
content, correlation, and W3C tracing headers. Credentials are disabled; correlation
ID and `Retry-After` are exposed. An unapproved origin gets no browser CORS permission;
CORS is not authentication. Preflight completes at the gateway. Downstream CORS
headers are removed so only the gateway policy applies.

`X-Correlation-ID` accepts exactly one nonempty GUID; invalid/multiple values generate
a new GUID. The normalized ID is consistent on request, response, and logs. The
existing error envelope's `traceId` field retains the repository convention of using
this request identifier. W3C trace IDs and span IDs are separate structured fields;
the trace ID survives while child span IDs change across hops.

Backend bodies/statuses/headers pass through, including empty errors, redirects,
`Retry-After`, and authentication challenges. Proxy transport headers follow YARP
rules. Gateway-generated errors use the existing `ApiResponse` envelope with static
`Gateway.*` codes: 502 forwarding failure, 503 no destination, 504 deadline/activity
timeout, 413 oversized body, and 500 unexpected failure. Responses already started
are aborted on forwarding failure. Client disconnects cancel downstream work.
No automatic retries or redirect following are enabled.

## Health and telemetry

`/health/live` checks the host; `/health/ready` checks initialized routing. They do
not query backends, databases, RabbitMQ, or telemetry. A backend outage affects its
routes without making the gateway unready. Monitor module readiness separately.

Use the existing telemetry flag to export gateway telemetry. The overview dashboard
includes gateway rate, p95 latency, result statuses, and forwarding failures. Completion
logs contain method, configured route/cluster/destination identifiers, status, latency,
correlation ID, trace/span IDs, and error codes. IDs are not metric labels. Gateway
filters remove raw URLs, query strings, headers, bodies, and exception payloads from
local logs and exports; collector allowlists retain only reviewed operational fields.

## Verify and release

```powershell
dotnet test tests/DevFlow.Gateway.Tests
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Architecture.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/Test-Deployment.ps1 -Config deploy/env/verification.env
```

Use a disposable `devflow-verify-*` stack, unique ports/subnet, and dedicated secrets.
The deployment harness checks gateway routes, direct local access, internal DNS,
outage isolation, module independence, and durable registration email delivery.
Production TLS/forwarding acceptance must also run through the actual ingress; a
Compose parse or loopback test cannot prove the operator's NAT/ingress settings.

Publish immutable application/setup images, deploy backends and gateway, verify
staging routes/CORS/forwarding/telemetry, then switch frontend API base URL and the
provider callback URL. No database migration is introduced by the gateway. Rollback
restores the prior gateway image and routing file, keeping backend ports private.
The current template remains single-host and requires an operator-managed ingress,
TLS broker, backup, alert routing, and recovery procedure.

## Verification evidence

Verified on 2026-10-28 with .NET SDK 10.0.401, Docker 29.7.2, and Compose 5.5.1:

- 48 gateway transport/configuration tests passed, using real loopback destinations.
  Architecture checks passed for all 25 source projects.
- Windows disposable deployment passed route isolation, server models without API
  ports, internal aliases, backend outage isolation, and production-mode forwarding.
  Identity's actual Redis IP bucket verified the original client IP; requests without
  HTTPS forwarding still redirected. Registration reached Mailpit through real workers.
- The Windows acceptance harness passed repeated startup, module independence,
  delivery/receipts/deduplication, role isolation, invalid settings, occupied ports,
  full shutdown, and retained-volume restart. Its final run used
  `-SkipFailureInjection -SkipHostMode`; Identity IDE execution was verified in an
  earlier run, and gateway IDE endpoint generation was checked separately. Broker
  credential/migration fault injection was not repeated for this change.
- The signed webhook integration test passed against disposable PostgreSQL databases.
- Linux Bash ran on a Docker-hosted Linux filesystem: full startup, registration
  delivery, direct API access, independent Identity redeployment, and full shutdown
  passed. Native Linux IDE execution was not exercised.
- A fresh gateway-only stack became ready without APIs, databases, workers, or
  RabbitMQ. Server gateway preflight passed; invalid IPs and overlapping Docker
  subnets were rejected.
- Live gateway completion fields reached Loki, histogram/counter metrics reached the
  collector, and W3C traces reached Tempo. Private query/body/bearer/signature markers
  were absent; trace/correlation IDs were absent from metric and indexed log labels.

Existing analyzer warnings remain in the solution. The normal solution run skips
dependency-specific tests without their opt-in environment; the deployed flow and
webhook checks above supplied their required environments separately. No external
staging/public ingress, certificate renewal, or real Resend delivery was exercised.
