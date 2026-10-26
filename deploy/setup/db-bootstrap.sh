#!/usr/bin/env bash
set -euo pipefail
module="${1:?Module required}"
case "$module" in
  identity) database=DevFlowIdentity; roles='identity_migrator identity_api identity_worker' ;;
  notification) database=DevFlowNotification; roles='notification_migrator notification_runtime' ;;
  *) exit 2 ;;
esac
export PGPASSWORD="$(cat "/source/$module/admin-password.secret")"
export PGHOST=postgres PGUSER="${module}_admin"
for role in $roles; do
  case "$role" in *_migrator) file=migrator-password;; *_api) file=api-password;; *_worker) file=worker-password;; *) file=runtime-password;; esac
  password="$(cat "/source/$module/$file.secret")"
  [[ "$password" =~ ^[0-9a-fA-F]{64}$ ]] || { echo "Database credentials must be 32-byte random hex values." >&2; exit 1; }
  # Secret values go through standard input, never process arguments or stdout.
  { printf "\set role '%s'\n\set password '%s'\n" "$role" "$password"; cat <<'SQL'
SELECT format('CREATE ROLE %I LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE PASSWORD %L', :'role', :'password') WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'role') \gexec
SQL
  } | psql -X -q -v ON_ERROR_STOP=1 -d postgres >/dev/null
 done
psql -X -q -v ON_ERROR_STOP=1 -d postgres -v database="$database" -v owner="${module}_migrator" <<'SQL' >/dev/null
SELECT format('CREATE DATABASE %I OWNER %I', :'database', :'owner') WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = :'database') \gexec
SELECT format('REVOKE ALL ON DATABASE %I FROM PUBLIC', :'database') \gexec
SQL
for role in $roles; do
 psql -X -q -v ON_ERROR_STOP=1 -d postgres -v database="$database" -v role="$role" <<'SQL' >/dev/null
SELECT format('GRANT CONNECT ON DATABASE %I TO %I', :'database', :'role') \gexec
SQL
 done
psql -X -q -v ON_ERROR_STOP=1 -d "$database" -c 'REVOKE CREATE ON SCHEMA public FROM PUBLIC;' >/dev/null
printf '%s\n' 'Module database and login roles are ready.'
