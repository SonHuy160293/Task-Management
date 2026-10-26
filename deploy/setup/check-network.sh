#!/usr/bin/env bash
# Read Docker network inspect JSON from stdin; no Docker socket or secrets are mounted.
set -euo pipefail
fail() { printf '%s\n' "$*" >&2; exit 1; }
ipv4() {
  local input=$1 a b c d extra
  IFS=. read -r a b c d extra <<<"$input"
  [[ -z ${extra:-} && $a =~ ^[0-9]{1,3}$ && $b =~ ^[0-9]{1,3}$ && $c =~ ^[0-9]{1,3}$ && $d =~ ^[0-9]{1,3}$ ]] || fail 'Expected an IPv4 address.'
  ((10#$a < 256 && 10#$b < 256 && 10#$c < 256 && 10#$d < 256)) || fail 'Invalid IPv4 address.'
  printf '%s' "$(( (10#$a << 24) | (10#$b << 16) | (10#$c << 8) | 10#$d ))"
}
subnet=${DEVFLOW_HTTP_SUBNET:?Set DEVFLOW_HTTP_SUBNET}
[[ $subnet = */* ]] || fail 'HTTP subnet requires CIDR notation.'
prefix=${subnet#*/}
[[ $prefix =~ ^[0-9]+$ ]] && ((prefix >= 8 && prefix <= 29)) || fail 'HTTP subnet requires an IPv4 prefix from /8 through /29.'
network=$(ipv4 "${subnet%/*}")
mask=$(( (0xffffffff << (32-prefix)) & 0xffffffff ))
(( (network & mask) == network )) || fail 'HTTP subnet must be a network address.'
last=$(( network | (0xffffffff ^ mask) ))
gateway=$(ipv4 "${DEVFLOW_GATEWAY_IP:?Set DEVFLOW_GATEWAY_IP}")
((gateway > network+1 && gateway < last)) || fail 'Gateway IP must be a usable subnet address, excluding the bridge address.'
if [[ -n ${DEVFLOW_INGRESS_PROXY_IP:-} ]]; then ipv4 "$DEVFLOW_INGRESS_PROXY_IP" >/dev/null; fi
while IFS='|' read -r name existing; do
  [[ -n $existing && $existing != *:* ]] || continue
  existing_prefix=${existing#*/}
  existing_network=$(ipv4 "${existing%/*}")
  existing_mask=$(( (0xffffffff << (32-existing_prefix)) & 0xffffffff ))
  existing_last=$(( existing_network | (0xffffffff ^ existing_mask) ))
  if [[ $name = "${DEVFLOW_STACK:?}-http" ]]; then
    [[ $existing = "$subnet" ]] || fail 'The existing stack HTTP network has a different subnet. Stop the stack before changing it.'
  elif (( network <= existing_last && existing_network <= last )); then
    fail "HTTP subnet overlaps Docker network $name. Choose another DEVFLOW_HTTP_SUBNET and DEVFLOW_GATEWAY_IP."
  fi
done < <(jq -r '.[] | .Name as $name | .IPAM.Config[]? | [$name, .Subnet] | join("|")')
