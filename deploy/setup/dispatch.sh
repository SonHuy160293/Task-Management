#!/usr/bin/env bash
set -euo pipefail
command="${1:-}"
shift || true
case "$command" in
  init-secrets|stage-secrets|db-bootstrap|db-grants|broker-bootstrap|host-settings|check-network) exec "/opt/devflow/setup/$command.sh" "$@" ;;
  network-init) exit 0 ;;
  *) echo 'Unknown deployment setup action.' >&2; exit 2 ;;
esac
