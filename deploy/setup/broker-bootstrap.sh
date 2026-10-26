#!/usr/bin/env bash
set -euo pipefail
umask 077
admin_password="$(cat /source/platform/broker-admin-password.secret)"
auth_file="$(mktemp)"
trap 'rm -f "$auth_file"' EXIT
printf 'user = "devflow-admin:%s"\n' "$admin_password" > "$auth_file"
request() { curl --silent --show-error --fail --config "$auth_file" --connect-timeout 5 --max-time 15 -H 'Content-Type: application/json' "$@" >/dev/null; }
base=http://rabbitmq:15672/api
vhost="$(jq -nr --arg vhost "${DEVFLOW_MESSAGING_VIRTUAL_HOST:-devflow}" '$vhost|@uri')"
request -X PUT "$base/vhosts/$vhost" -d '{}'
for module in identity notification; do
 jq -n --rawfile password "/source/platform/$module-broker-password.secret" '{password:($password|rtrimstr("\n")),tags:""}' | request -X PUT "$base/users/$module" --data-binary @-
 read_pattern="^devflow\.$module\."
 # Exchange binding requires read permission on the incoming receipt exchange.
 # This exact exchange exception does not grant reads of Notification's inbox.
 if [[ "$module" == identity ]]; then read_pattern='(^devflow\.identity\.|^devflow\.notification\.email-recorded\.v1$)'; fi
 jq -n --arg namespace '^devflow\.' --arg read "$read_pattern" '{configure:$namespace,write:$namespace,read:$read}' | request -X PUT "$base/permissions/$vhost/$module" --data-binary @-
done
jq -n --arg namespace '^devflow\.' '{pattern:$namespace,definition:{overflow:"reject-publish","max-length":100000,"delivery-limit":20,"dead-letter-exchange":"devflow.messaging.unrouted","dead-letter-strategy":"at-least-once"},priority:10,"apply-to":"quorum_queues"}' | request -X PUT "$base/policies/$vhost/delivery-safety" --data-binary @-
printf '%s\n' 'Broker users, permissions, and safety policy are ready.'
