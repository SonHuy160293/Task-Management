#!/usr/bin/env sh
set -eu
file="${DEVFLOW_WORKER_HEARTBEAT_FILE:-/tmp/devflow-worker.heartbeat}"
test -f "$file"
now=$(date +%s)
last=$(stat -c %Y "$file")
age=$((now - last))
test "$age" -ge 0 && test "$age" -le 180
