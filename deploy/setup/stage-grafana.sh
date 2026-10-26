#!/usr/bin/env bash
set -euo pipefail
umask 077
test -s /source/platform/grafana-admin-password.secret || { echo 'Grafana admin secret is missing.' >&2; exit 1; }
install -d -m 700 -o 472 -g 472 /runtime
install -m 400 -o 472 -g 472 /source/platform/grafana-admin-password.secret /runtime/admin-password.tmp
mv -f /runtime/admin-password.tmp /runtime/admin-password
echo 'Grafana credential staged with restricted ownership.'
