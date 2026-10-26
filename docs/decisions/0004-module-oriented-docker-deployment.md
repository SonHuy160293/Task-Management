# Deploy each module independently behind one onboarding interface

Status: accepted, 2026-10-26. Supersedes local deployment layout choices in
[ADR 0001](0001-identity-migration-runner.md) and
[ADR 0003](0003-durable-notification-delivery.md); their application, migration,
transaction, and messaging decisions remain in force.

The isolated learning stacks required teammates to coordinate multiple environment
files, broker commands, grants, migrations, and ports, and placed Identity's HTTP
host and worker on different databases. We choose a platform Compose project and
independently operated Identity and Notification projects, with one PostgreSQL
container/volume per module and the same owning database for its HTTP host and
worker. A small Windows/Linux onboarding interface owns validation and ordering;
Compose/setup jobs own the implementation so deployment tests exercise the same
interface as teammates.

Local operation supports full Docker and infrastructure-only IDE execution from
one nonsecret settings source and protected source secret files. Credentials are
staged into restricted runtime-owned volumes, migrations have separate credentials,
and missing local secrets are generated without replacing existing material. The
Linux template pulls immutable images, keeps HTTP private pending external TLS
ingress, and uses an operator-provided trusted TLS RabbitMQ cluster. Local Mailpit
is the default; telemetry is opt-in and server email sending is Disabled until
explicitly configured.

This chooses module storage/lifecycle independence over the smaller footprint of
one PostgreSQL process, and a repeatable interface over manual cross-project command
ordering. It does not imply a gateway, new business flows, multi-host orchestration,
or high availability. Fresh project/volume names leave legacy data untouched;
import requires a reviewed backup/restore transition. Historical ADRs remain
unchanged.
