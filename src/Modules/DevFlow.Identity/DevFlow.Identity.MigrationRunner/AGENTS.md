# Identity migration runner

Root and Identity module instructions apply here. This executable is a one-shot
composition root referencing Identity Infrastructure and BuildingBlocks only.
Register persistence without API security, Redis, repositories, or CQRS handlers.
Apply migrations with cancellation, return a nonzero exit code on failure, and
dispose the host so telemetry can flush. Never log connection strings or raw
provider exceptions. Do not introduce seeding or API-startup migrations.

Migration history and the snapshot belong in Identity Infrastructure under
`Persistence/Migrations`; operating instructions belong in
`docs/identity/MIGRATIONS.md` and `deploy/README.md`.
