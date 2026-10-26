#!/usr/bin/env bash
set -euo pipefail
umask 077
module="${1:?Module required}"
copy_secret() {
  local source="$1" target="$2" owner="${3:-1654}"
  test -f "$source" || { echo "Missing source secret: ${source##*/}" >&2; exit 1; }
  mkdir -p "$(dirname "$target")"
  cp "$source" "$target.tmp"
  chown "$owner:$owner" "$target.tmp"
  chmod 400 "$target.tmp"
  mv -f "$target.tmp" "$target"
  chown "$owner:$owner" "$(dirname "$target")"
  chmod 700 "$(dirname "$target")"
}
if [[ "$module" == platform ]]; then
  copy_secret /source/platform/broker-admin-password.secret /staged/broker/broker-admin-password 999
  copy_secret /source/platform/broker-admin-password.secret /staged/topology/broker-admin-password
  chown 999:999 /staged/broker; chmod 700 /staged/broker
  chown 1654:1654 /staged/topology; chmod 700 /staged/topology
  printf '%s\n' 'Platform secrets staged with restricted ownership.'
  exit 0
elif [[ "$module" == identity ]]; then
  active_key=${DEVFLOW_EMAIL_ACTIVE_KEY_ID:-local-v1}
  [[ $active_key =~ ^[A-Za-z0-9-]{1,64}$ ]] || { echo 'Invalid email key ID.' >&2; exit 1; }
  copy_secret "/source/identity/notification-keys/$active_key.public.pem" /staged/api/verification-public.pem
  copy_secret /source/identity/admin-password.secret /staged/database/admin-password 999
  copy_secret /source/identity/migrator-password.secret /staged/migration/database-password
  copy_secret /source/identity/api-password.secret /staged/api/database-password
  copy_secret /source/identity/worker-password.secret /staged/worker/database-password
  copy_secret /source/platform/identity-broker-password.secret /staged/worker/broker-password
  copy_secret /source/identity/redis-password.secret /staged/redis/password 999
  copy_secret /source/identity/jwt-key.secret /staged/api/config/Jwt__SecretKey
  copy_secret /source/identity/integration-key.secret /staged/api/config/IntegrationApi__ApiKeys__0
  copy_secret /source/identity/registration-hmac.secret /staged/api/config/Identity__Security__RegistrationRateLimit__KeySecretBase64
  # Text credentials created by older initialization scripts may end with LF.
  # Match command-substitution/IDE semantics without modifying source files.
  for file in /staged/api/config/Jwt__SecretKey /staged/api/config/IntegrationApi__ApiKeys__0 /staged/api/config/Identity__Security__RegistrationRateLimit__KeySecretBase64; do
    value="$(tr -d '\r\n' < "$file")"
    printf '%s' "$value" > "$file"
  done
  redis_password="$(cat /source/identity/redis-password.secret)"
  mkdir -p /staged/api/config
  printf 'redis:6379,password=%s,abortConnect=false' "$redis_password" > /staged/api/config/Identity__Security__RegistrationRateLimit__RedisConnectionString
  chown 1654:1654 /staged/api/config/Identity__Security__RegistrationRateLimit__RedisConnectionString
  chmod 400 /staged/api/config/Identity__Security__RegistrationRateLimit__RedisConnectionString
elif [[ "$module" == notification ]]; then
  copy_secret /source/notification/admin-password.secret /staged/database/admin-password 999
  copy_secret /source/notification/migrator-password.secret /staged/migration/database-password
  copy_secret /source/notification/runtime-password.secret /staged/api/database-password
  copy_secret /source/notification/runtime-password.secret /staged/worker/database-password
  copy_secret /source/platform/notification-broker-password.secret /staged/worker/broker-password
  copy_secret /source/notification/keys/recipient-hmac.secret /staged/api/recipient-hmac.secret
  for name in resend-api.secret resend-webhook.secret; do
    if [[ -f "/source/notification/keys/$name" ]]; then
      copy_secret "/source/notification/keys/$name" "/staged/worker/keys/$name"
    else
      mkdir -p /staged/worker/keys
      : > "/staged/worker/keys/$name"
      chown 1654:1654 "/staged/worker/keys/$name"
      chmod 400 "/staged/worker/keys/$name"
    fi
  done
  if [[ -f /source/notification/keys/resend-webhook.secret ]]; then
    copy_secret /source/notification/keys/resend-webhook.secret /staged/api/resend-webhook.secret
  else
    : > /staged/api/resend-webhook.secret
    chown 1654:1654 /staged/api/resend-webhook.secret
    chmod 400 /staged/api/resend-webhook.secret
  fi
  for source in /source/notification/keys/*; do copy_secret "$source" "/staged/worker/keys/${source##*/}"; done
else
  echo 'Unsupported module.' >&2; exit 2
fi
# Volume roots must also be traversable by the intended non-root consumer.
for name in api worker migration; do chown 1654:1654 "/staged/$name"; chmod 700 "/staged/$name"; done
chown 999:999 /staged/database; chmod 700 /staged/database
printf '%s\n' 'Consumer-specific secrets staged with restricted ownership.'
