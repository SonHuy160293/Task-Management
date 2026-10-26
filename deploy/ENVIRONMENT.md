# Environment and secret reference

Use `scripts/devflow.ps1 init` or `bash scripts/devflow.sh init`. The entry point
copies `deploy/env/local.env.example` to ignored `local.env` once; server mode
copies `server.env.example` to `server.env` but does not create server credentials.
Custom config files are selected with `-Config`/`--config`.

## Configuration contract

- Environment files contain **nonsecret literal, unquoted `KEY=value`** entries.
  Blank lines and full-line comments are supported. Duplicate keys, shell
  expansion, placeholders, and invalid selected ports fail preflight. Do not put
  passwords or private-key contents in these files.
- Entry points explicitly load their selected file into the process environment.
  Raw `dotnet run` does not load `.env` files; raw Compose `--env-file` is an
  interpolation source, not a host-shell export.
- `DEVFLOW_*` names configure deployment. They are mapped to application settings
  by Compose/setup jobs. For example, `DEVFLOW_JWT_ISSUER` becomes `Jwt__Issuer`.
  Arrays in .NET configuration use numeric indexes such as `Cors__AllowedOrigins__0`.
- Keep Development/Production aligned across host settings. Messaging and email
  have their own explicit development flags; `DOTNET_ENVIRONMENT=Development`
  alone does not enable either development adapter.
- Container DNS/internal ports and host loopback/published ports are distinct.
  IDE mode derives host configuration from the same settings and source secrets.
  Refresh host mode after changing them.
- Linux source secrets must not grant access to other users. Setup stages only
  the necessary files into consumer-specific named volumes with restricted
  ownership. Runtime mounts are read-only; do not relax private-key permissions.

## Deployment variables

Defaults below describe `local.env.example`; server differences are explicit.

| Variable | Default / rule |
| --- | --- |
| `DEVFLOW_STACK` | `devflow-local`; server `devflow-server`. Must start `devflow-` and use lowercase letters, digits, and hyphens. Determines isolated project/network/volume names. |
| `DEVFLOW_ENVIRONMENT` | `Development`; server requires `Production`. |
| `BUILDX_BUILDER` | Optional local build override; unset in the example. Selects a named Docker Buildx builder for onboarding builds. A value in the selected env file overrides the inherited process value. See [build-cache recovery](OPERATIONS.md#recover-from-a-missing-docker-build-snapshot). |
| `DEVFLOW_SECRETS_DIR` | `deploy/secrets/devflow-local`; server `/etc/devflow/secrets`. Relative paths resolve against the repository. |
| `DEVFLOW_GATEWAY_IMAGE` | `devflow-gateway:local`; server immutable release image. |
| `DEVFLOW_GATEWAY_PORT` | `8080`, host loopback listener. |
| `DEVFLOW_GATEWAY_PUBLIC_HOST` | `localhost`; server public API hostname. |
| `DEVFLOW_HTTP_SUBNET` | `172.30.250.0/24`; no Docker network overlap. |
| `DEVFLOW_GATEWAY_IP` | `172.30.250.10`; fixed usable HTTP-network address. |
| `DEVFLOW_INGRESS_PROXY_IP` | `172.30.250.1` locally; server actual ingress peer IPv4. |
| `DEVFLOW_SETUP_IMAGE` | `devflow-setup:local`; server requires a published immutable release image. |
| `DEVFLOW_IDENTITY_API_IMAGE` | `devflow-identity-api:local`; corresponding server release image. |
| `DEVFLOW_IDENTITY_WORKER_IMAGE` | `devflow-identity-worker:local`; corresponding server release image. |
| `DEVFLOW_IDENTITY_MIGRATION_IMAGE` | `devflow-identity-migration:local`; corresponding server release image. |
| `DEVFLOW_NOTIFICATION_API_IMAGE` | `devflow-notification-api:local`; corresponding server release image. |
| `DEVFLOW_NOTIFICATION_WORKER_IMAGE` | `devflow-notification-worker:local`; corresponding server release image. |
| `DEVFLOW_NOTIFICATION_MIGRATION_IMAGE` | `devflow-notification-migration:local`; corresponding server release image. |
| `DEVFLOW_DOTNET_SDK_IMAGE` | Reviewed .NET 10 SDK digest used for local builds. |
| `DEVFLOW_DOTNET_RUNTIME_IMAGE` | Reviewed .NET 10 ASP.NET runtime digest. |
| `DEVFLOW_POSTGRES_IMAGE` | Reviewed PostgreSQL 18 digest; PostgreSQL 17 is unsuitable for Identity's schema. |
| `DEVFLOW_REDIS_IMAGE` | Reviewed Redis 7.4 image digest. |
| `DEVFLOW_RABBITMQ_IMAGE` | Pinned RabbitMQ 4.3.6 management image for local infrastructure. |
| `DEVFLOW_MAILPIT_IMAGE` | Pinned Mailpit image for local capture. |
| `DEVFLOW_IDENTITY_API_PORT` | `8081`; local-only module HTTP listener on loopback. |
| `DEVFLOW_NOTIFICATION_API_PORT` | `8082`; local-only module HTTP listener on loopback. |
| `DEVFLOW_IDENTITY_POSTGRES_PORT` | `15431`; local developer access only. |
| `DEVFLOW_NOTIFICATION_POSTGRES_PORT` | `15432`; local developer access only. |
| `DEVFLOW_REDIS_PORT` | `16379`; local Identity Redis access. |
| `DEVFLOW_RABBITMQ_AMQP_PORT` | `5672`; local host AMQP access. |
| `DEVFLOW_RABBITMQ_MANAGEMENT_PORT` | `15672`; local host management access. |
| `DEVFLOW_MAILPIT_SMTP_PORT` | `11025`; local host SMTP access. |
| `DEVFLOW_MAILPIT_HTTP_PORT` | `18025`; local mailbox UI. |
| `DEVFLOW_FRONTEND_ORIGIN` | `http://localhost:3000`; server requires the intended HTTPS origin. No path/query/fragment/credentials. |
| `DEVFLOW_JWT_ISSUER` | `DevFlow.Identity`; nonempty. |
| `DEVFLOW_JWT_AUDIENCE` | `DevFlow`; nonempty. |
| `DEVFLOW_MESSAGING_HOST` | `rabbitmq` locally; server requires an operator-provided TLS broker host. |
| `DEVFLOW_MESSAGING_PORT` | `5672` locally, `5671` in the server example; broker internal/reachable port, not the local publication setting. |
| `DEVFLOW_MESSAGING_VIRTUAL_HOST` | `/devflow-local`; server example `/devflow-server`. Must match configured permissions and topology. |
| `DEVFLOW_MESSAGING_DEVELOPMENT` | `true` locally; server requires `false`. |
| `DEVFLOW_MESSAGING_USE_TLS` | `false` locally; server requires `true` with normal certificate trust. |
| `DEVFLOW_MESSAGING_QUORUM_SIZE` | `1` locally; server requires 3–7. Actual fault tolerance also requires a properly deployed cluster. |
| `DEVFLOW_EMAIL_MODE` | `Mailpit` locally; `Disabled` on the server. Supported modes: `Disabled`, `Mailpit`, `Resend`. |
| `DEVFLOW_EMAIL_SENDER` | `DevFlow <auth@example.test>` locally; valid mailbox and verified domain for Resend. |
| `DEVFLOW_EMAIL_DOMAIN_VERIFIED` | `false`; set true only after actual sender-domain verification. |
| `DEVFLOW_EMAIL_ACTIVE_KEY_ID` | Optional, `local-v1`; immutable RSA key ID, 1–64 ASCII letters/digits/hyphens. Both matching PEM files must exist. |
| `DEVFLOW_EMAIL_DAILY_LIMIT`, `DEVFLOW_EMAIL_MONTHLY_LIMIT` | Optional, 100 / 3000; sending quota limits, including retry reservations. |
| `DEVFLOW_MAILPIT_HOST`, `DEVFLOW_MAILPIT_PORT` | Optional container SMTP endpoint, `mailpit:1025`; IDE configuration uses host loopback and `DEVFLOW_MAILPIT_SMTP_PORT`. |
| `DEVFLOW_GRAFANA_PORT` | `13000`; optional telemetry UI. |
| `DEVFLOW_OTLP_PORT` | `4317`; optional OTLP/gRPC ingestion. |
| `DEVFLOW_OTLP_HTTP_PORT` | `4318`; optional OTLP/HTTP ingestion. |
| `DEVFLOW_COLLECTOR_IMAGE` | Collector image selected by the telemetry model. |
| `DEVFLOW_GRAFANA_IMAGE` | Grafana image selected by the telemetry model. |
| `DEVFLOW_LOKI_IMAGE` | Loki image selected by the telemetry model. |
| `DEVFLOW_TEMPO_IMAGE` | Tempo image selected by the telemetry model. |
| `DEVFLOW_PROMETHEUS_IMAGE` | Prometheus image selected by the telemetry model. |
| `DEVFLOW_OBSERVABILITY_ENABLED` | Derived by the entry point from `-Telemetry`/`--telemetry`; default false. |

Port numbers must be 1–65535 and selected publications must not conflict. Server
module definitions publish no ports; only gateway publishes loopback HTTP.
Developer database, Redis, broker, and Mailpit publication values are not used there.
Server application/setup images
must use release tags or digests; `latest`, `local`, `dev`, and `main` are rejected.

## Source secret files

All paths below are relative to `DEVFLOW_SECRETS_DIR`. Locally, `init` generates
independent random database/broker passwords, Base64 keys, and an RSA-3072 pair.
Existing files are retained. An incomplete encryption pair must be repaired from
backup rather than silently regenerated. Server files are operator-provisioned.

| Path | Purpose / rule |
| --- | --- |
| `platform/broker-admin-password.secret` | Local broker setup only; never mounted into application runtimes. |
| `platform/identity-broker-password.secret` | Identity worker's broker account; server value must match the external broker. |
| `platform/notification-broker-password.secret` | Notification worker's broker account; server value must match the external broker. |
| `platform/grafana-admin-password.secret` | Required when enabling telemetry; keep independent from application credentials. |
| `identity/admin-password.secret` | PostgreSQL administration/bootstrap, separate from runtime. |
| `identity/migrator-password.secret` | Identity schema owner/migration credential. |
| `identity/api-password.secret` | Identity HTTP runtime credential. |
| `identity/worker-password.secret` | Identity worker credential with reliability permissions only. |
| `identity/redis-password.secret` | Identity Redis authentication. |
| `identity/registration-hmac.secret` | Base64 decoding to 32–64 bytes; stable across rate-limit replicas/restarts. |
| `identity/jwt-key.secret` | Independent signing key, at least 32 characters. |
| `identity/integration-key.secret` | Nonempty incoming integration key, at most 4096 characters. |
| `notification/admin-password.secret` | Notification PostgreSQL administration/bootstrap. |
| `notification/migrator-password.secret` | Notification schema owner/migration credential. |
| `notification/runtime-password.secret` | Notification HTTP/worker runtime credential. |
| `notification/keys/local-v1.public.pem` | Initial RSA public key. |
| `identity/notification-keys/<key-id>.public.pem` | Identity-owned copy of the selected public key; provision independently on servers. |
| `notification/keys/local-v1.private.pem` | Initial RSA private key; Notification worker only. |
| `notification/keys/recipient-hmac.secret` | Base64 decoding to at least 32 bytes; required for Notification intake, including Disabled mode. |
| `notification/keys/resend-api.secret` | Real Resend API key when that mode is enabled. Local init creates an empty placeholder. |
| `notification/keys/resend-webhook.secret` | Resend callback signing secret; local init creates an empty placeholder. |

Provider secret files must be non-whitespace text and at most 8192 bytes. Empty
Resend placeholders are acceptable only while Resend is unused. The Notification
HTTP host receives callback/HMAC material but no private encryption key or provider
API key. Staged volumes are credential delivery, not the backup source of truth.
The active RSA pair and recipient-HMAC file are provisioned even in Disabled mode:
intake still validates and durably records encrypted jobs. Additional retained key
pairs remain available to the worker for pending requests after key rotation.

## Application configuration

Normal onboarding generates these settings. Set them directly only for a deliberate
custom hosting arrangement. .NET nested names use `__` in environment variables
and `:` in user-secrets keys.

Identity HTTP loads `/run/secrets/config` through the key-per-file provider before
registration. File values override JSON, environment variables, and command-line
settings for the same key. Files are read at startup; restart after changing them.
Without that directory, existing environment/user-secrets behavior remains valid.
The provider directory can be overridden by `DEVFLOW_CONFIG_SECRETS_DIRECTORY`.

| Setting | Consumer / rule |
| --- | --- |
| `ConnectionStrings__ApplicationDb` | Identity API, worker, and runner: owning PostgreSQL connection. |
| `ConnectionStrings__NotificationDb` | Notification API, worker, and runner: owning PostgreSQL connection. |
| `Database__PasswordFile` | Readable owning database password file; overrides the connection string's password. |
| `Jwt__SecretKey`, `Jwt__Issuer`, `Jwt__Audience` | Identity HTTP requires nonempty issuer/audience and a ≥32-character signing key. |
| `IntegrationApi__ApiKeys__0` | Identity HTTP requires at least one nonempty key, each ≤4096 characters. More keys use `__1`, etc. |
| `IntegrationApi__HeaderName` | Default `X-Integration-Key`; callers must match it. |
| `Identity__Security__RegistrationRateLimit__RedisConnectionString` | Identity HTTP: reachable authenticated Redis endpoint. |
| `Identity__Security__RegistrationRateLimit__KeySecretBase64` | Identity HTTP: Base64 encoding of 32–64 bytes. |
| `Cors__AllowedOrigins__0` | Explicit browser origin; wildcard origins are rejected. Add indexes for additional origins. |
| `Cors__AllowedMethods__0`, `Cors__AllowedHeaders__0` | Nonempty arrays; generated settings allow GET/POST and Content-Type/Authorization. |
| `Messaging__Host`, `Messaging__Port`, `Messaging__VirtualHost`, `Messaging__Username` | Workers: reachable broker/vhost/account matching provisioning. |
| `Messaging__PasswordFile` | Worker broker password file. There is no raw `Messaging__Password` option. |
| `Messaging__Development`, `Messaging__UseTls`, `Messaging__QuorumSize` | Explicit local development or non-development TLS with quorum size 3–7. |
| `Notification__EmailDelivery__Mode` | Disabled, Mailpit, or Resend. Mailpit requires `Development=true`. |
| `Notification__EmailDelivery__Development` | Explicit email development flag; not inferred from host environment. |
| `Notification__EmailDelivery__Sender`, `Notification__EmailDelivery__FrontendOrigin` | Valid mailbox and trusted origin. Non-loopback/non-development email links require HTTPS. |
| `Notification__EmailDelivery__KeyDirectory`, `Notification__EmailDelivery__ActiveKeyId` | Worker key directory and immutable ID; matching public/private files required for sending. |
| `Notification__EmailDelivery__RecipientHashKeyFile` | Stable suppression HMAC file. |
| `Notification__EmailDelivery__ApiKeyFile`, `Notification__EmailDelivery__WebhookSecretFile` | Resend worker/callback credentials. |
| `Notification__EmailDelivery__DomainVerified` | Must be true for Resend sending. |
| `Notification__EmailDelivery__MailpitHost`, `Notification__EmailDelivery__MailpitPort` | Worker SMTP endpoint; generated host and container settings differ intentionally. |
| `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT` | Development locally, Production on the server. Keep aligned. |
| `ASPNETCORE_URLS`, `ASPNETCORE_HTTP_PORTS` | Listener configuration; generated profiles/models handle these. Explicit URLs take precedence over port-only settings. |
| `DEVFLOW_WORKER_HEARTBEAT_FILE` | Optional outbox successful-scan heartbeat path; Docker uses `/tmp/devflow-worker.heartbeat`. |

Migration runners need only their database configuration. Identity's worker does
not load the HTTP registration/password/JWT options. Notification HTTP does not
register broker transport or require RSA private keys.

### Optional application tuning

These retain their source defaults unless deliberately overridden in loaded .NET
configuration. The deployment env parser accepts deployment keys, not arbitrary
raw .NET keys; use a reviewed Compose override or the appropriate user-secrets store.

| Variable | Default / validated range |
| --- | --- |
| `Jwt__AccessTokenExpirationMinutes` | 60; positive integer. |
| `Cors__ExposedHeaders__0` | Empty list. |
| `Cors__AllowCredentials` | false; intended credentialed requests with explicit origins only. |
| `Cors__PreflightMaxAgeMinutes` | 10; integer ≥0. |
| `ForwardedHeaders__KnownProxies__0` | Empty configured list; add only actual ingress proxy IPs. |
| `PasswordReset__UseFrontendResetPage`, `PasswordReset__EnableBuiltInResetPage` | false; optional page-routing flags, not an implemented reset flow. |
| `Identity__Security__RegistrationRateLimit__IpPermitLimit` | 20; 1–10000. |
| `Identity__Security__RegistrationRateLimit__IpWindow` | 00:10:00; 1 second–1 day. |
| `Identity__Security__RegistrationRateLimit__EmailPermitLimit` | 5; 1–10000. |
| `Identity__Security__RegistrationRateLimit__EmailWindow` | 01:00:00; 1 second–1 day. |
| `Identity__Security__Argon2__MemoryKiB` | 65536; 19456–131072. |
| `Identity__Security__Argon2__Iterations` | 3; 2–6. |
| `Identity__Security__Argon2__Parallelism` | 1; 1–4. |
| `Identity__Security__Argon2__MaxConcurrency` | 2; 1–16. |
| `Identity__Security__VerificationToken__Lifetime` | 1.00:00:00; 5 minutes–24 hours. |
| `Notification__EmailDelivery__DailyLimit` | 100; 1–100. Retry submissions also reserve quota. |
| `Notification__EmailDelivery__MonthlyLimit` | 3000; 1–3000, tracked in UTC. |
| `Notification__EmailDelivery__SupportAddress` | Empty; currently not rendered by the template. |
| `Serilog__Using__0`, `Serilog__WriteTo__0__Name` | Generated Identity console logging: `Serilog.Sinks.Console` / `Console`. |
| `Observability__Enabled` | Selected by the telemetry option; disabled in normal onboarding. |
| `OTEL_EXPORTER_OTLP_ENDPOINT`, `Observability__Endpoint` | OTLP/gRPC endpoint; OTEL key wins. Source endpoint default `http://localhost:4317`. |
| `OTEL_SERVICE_NAME`, `Observability__ServiceName` | OTEL key wins; otherwise the host application name. |
| `Observability__ServiceVersion` | Unset optional telemetry version. |
| `Observability__TraceSamplingRatio` | 1; clamped to 0–1. |
| `Observability__ExportTimeoutMilliseconds` | 3000; clamped to 500–10000 ms. |
| `Observability__MetricExportIntervalMilliseconds` | 10000; clamped to 1000–60000 ms. |

## Test-only variables

These are not application startup settings. Use disposable resources: tests write
rows, delivery fixtures create/drop databases, and recovery tests restart containers.

| Variable | Purpose |
| --- | --- |
| `DEVFLOW_TEST_POSTGRES` | Identity test database, PostgreSQL 18 with migrations applied. |
| `DEVFLOW_TEST_REDIS` | Disposable Redis endpoint for Identity tests. |
| `DEVFLOW_DEPLOYMENT_TEST_CONFIG` | Absolute or repository-relative env file for deployed-stack delivery, receipt, deduplication, and runtime-role checks; requires disposable Development/Mailpit infrastructure. |
| `DEVFLOW_DELIVERY_TEST_POSTGRES` | PostgreSQL 18 server connection with database-create/drop rights. |
| `DEVFLOW_DELIVERY_TEST_POSTGRES_CONTAINER` | PostgreSQL container name for dump/restore tests. |
| `DEVFLOW_DELIVERY_TEST_RABBIT_PORT` | Local RabbitMQ test port. |
| `DEVFLOW_DELIVERY_TEST_RABBIT_PASSWORD` | Broker user `devflow_test` password in test vhost `/`. |
| `DEVFLOW_DELIVERY_TEST_RABBIT_CONTAINER` | Broker container name for restart/recovery tests. |
| `DEVFLOW_DELIVERY_TEST_SMTP_PORT` | Local test SMTP port. |
| `DEVFLOW_DELIVERY_TEST_MAILPIT_URL` | Test Mailpit HTTP API URL. |

`scripts/Test-EmailDelivery.ps1` creates disposable services, configures its own
delivery-test variables, restores previous values, and cleans up its containers.
Unset external dependencies cause the corresponding direct `dotnet test` cases to
skip. Do not substitute production databases or queues.

Identity API requires `Identity__VerificationEmail__KeyId` and `Identity__VerificationEmail__PublicKeyFile`. Onboarding sets both from `DEVFLOW_EMAIL_ACTIVE_KEY_ID` and stages only that Identity-owned public key into the API volume. Local `init` copies Notification public keys without overwriting an existing ID and rejects differing copies. Run `init` once when upgrading an existing local deployment. For rotation, distribute the new public key before selecting its ID and restart Identity; retain prior private keys in Notification for pending messages.
