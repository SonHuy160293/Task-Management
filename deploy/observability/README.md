# Optional deployment telemetry

Enable telemetry through the supported onboarding interface:

```powershell
./scripts/devflow.ps1 up -Telemetry
./scripts/devflow.ps1 status -Telemetry
```

Linux uses `bash ./scripts/devflow.sh up --telemetry`. Select a module with
`-Module identity` / `--module identity` when needed. The entry point starts the
selected modules and a separate `<DEVFLOW_STACK>-telemetry` project. The collector
joins the platform network so hosts reach `http://otel-collector:4317`.

Grafana defaults to <http://localhost:13000/d/devflow-overview/devflow-overview>.
Host OTLP/gRPC and HTTP ingestion default to ports 4317 and 4318. All publications
bind to loopback; change them in the selected deployment environment file. IDE
mode receives the host collector endpoint in user-secrets. Repeat the telemetry
option when refreshing those stores or runtimes.

Initialization creates an independent Grafana administrator password in
`platform/grafana-admin-password.secret` under `DEVFLOW_SECRETS_DIR`. A setup job
stages it into a Grafana-owned read-only mount. It is not placed in the environment
or printed. Anonymous local access has Viewer permissions. Do not expose the
developer dashboard publicly without reviewed authentication and ingress.

Logs reach Loki, traces Tempo, and metrics Prometheus through the collector.
Storage retains seven days in named volumes. Dashboards include HTTP, CQRS,
runtime, trace, and gateway rate/latency/status/failure panels. Empty panels are expected until traffic exists.

Telemetry is not a module startup/readiness dependency. Queues, deadlines, and
sampling remain bounded; outages may lose telemetry but must not prevent the
module from serving its supported workload. Preserve the privacy processors and
collector allowlists: no credentials, tokens, recipients, raw bodies, raw SQL,
or arbitrary exception payloads. Trace/span IDs are structured metadata, not
indexed labels.

Configuration is in `deploy/compose/telemetry/compose.yaml`; lifecycle and recovery
follow [deployment operations](../OPERATIONS.md). Historical Compose overrides
were replaced by this module-oriented deployment.
