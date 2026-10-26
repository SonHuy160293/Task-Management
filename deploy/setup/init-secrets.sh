#!/usr/bin/env bash
set -euo pipefail
umask 077
mkdir -p /source/platform /source/identity /source/notification/keys
new_hex() { if [[ ! -e "$1" ]]; then openssl rand -hex 32 | tr -d '\n' > "$1"; fi; }
new_base64() { if [[ ! -e "$1" ]]; then openssl rand -base64 32 | tr -d '\n' > "$1"; fi; }
for name in broker-admin-password identity-broker-password notification-broker-password grafana-admin-password; do new_hex "/source/platform/$name.secret"; done
for name in admin-password migrator-password api-password worker-password redis-password; do new_hex "/source/identity/$name.secret"; done
for name in registration-hmac jwt-key integration-key; do new_base64 "/source/identity/$name.secret"; done
for name in admin-password migrator-password runtime-password; do new_hex "/source/notification/$name.secret"; done
keys=/source/notification/keys
if [[ ! -e "$keys/local-v1.private.pem" && ! -e "$keys/local-v1.public.pem" ]]; then
  openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:3072 -out "$keys/local-v1.private.pem" 2>/dev/null
  openssl pkey -in "$keys/local-v1.private.pem" -pubout -out "$keys/local-v1.public.pem" 2>/dev/null
elif [[ ! -s "$keys/local-v1.private.pem" || ! -s "$keys/local-v1.public.pem" ]]; then
  echo 'Incomplete encryption key pair; repair from backup without regenerating keys.' >&2; exit 1
fi
mkdir -p /source/identity/notification-keys
for public_key in "$keys"/*.public.pem; do
  [[ -f "$public_key" ]] || continue
  target="/source/identity/notification-keys/${public_key##*/}"
  if [[ -e "$target" ]]; then
    cmp -s "$public_key" "$target" || { echo 'A different public key already exists under this key ID.' >&2; exit 1; }
  else
    cp -n "$public_key" "$target"
  fi
done
new_base64 "$keys/recipient-hmac.secret"
for name in resend-api resend-webhook; do if [[ ! -e "$keys/$name.secret" ]]; then : > "$keys/$name.secret"; fi; done
printf '%s\n' 'Deployment secrets initialized; existing files were preserved.'
