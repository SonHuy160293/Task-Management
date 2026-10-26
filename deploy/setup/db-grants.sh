#!/usr/bin/env bash
set -euo pipefail
module="${1:?Module required}"
export PGPASSWORD="$(cat "/source/$module/admin-password.secret")"
export PGHOST=postgres PGUSER="${module}_admin"
if [[ "$module" == identity ]]; then
  psql -X -q -v ON_ERROR_STOP=1 -d DevFlowIdentity <<'SQL' >/dev/null
GRANT USAGE ON SCHEMA shared, dbo, reliability TO identity_api;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA shared, dbo, reliability TO identity_api;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA shared, dbo, reliability TO identity_api;
GRANT USAGE ON SCHEMA reliability TO identity_worker;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA reliability TO identity_worker;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA reliability TO identity_worker;
REVOKE UPDATE, DELETE ON reliability."OutboxOperations", reliability."OutboxRecoveries" FROM identity_api, identity_worker;
SQL
  # Audit tables vary by installed migration; retain append-only privileges when present.
  psql -X -q -v ON_ERROR_STOP=1 -d DevFlowIdentity <<'SQL' >/dev/null
SELECT format('REVOKE UPDATE, DELETE ON %I.%I FROM identity_api', schemaname, tablename) FROM pg_tables WHERE schemaname IN ('shared','dbo') AND tablename IN ('SecurityAuditEvents','SecurityAuditLogs') \gexec
SQL
elif [[ "$module" == notification ]]; then
  psql -X -q -v ON_ERROR_STOP=1 -d DevFlowNotification <<'SQL' >/dev/null
GRANT USAGE ON SCHEMA notification, reliability TO notification_runtime;
GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA notification, reliability TO notification_runtime;
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA notification, reliability TO notification_runtime;
REVOKE UPDATE, DELETE ON notification."EmailOperations", reliability."OutboxOperations" FROM notification_runtime;
SQL
else exit 2
fi
printf '%s\n' 'Runtime grants refreshed after migrations.'
