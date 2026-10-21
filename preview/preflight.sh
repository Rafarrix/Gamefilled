#!/usr/bin/env bash
set -euo pipefail

# Run from the repository root, before docker compose. Never print secrets.
env_file="preview/.env.preview"
secret_file="preview/local/secrets/resend_api_key"
keys_dir="preview/local/keys"

fail() { printf 'Preview preflight failed: %s\n' "$1" >&2; exit 1; }

[[ -f "$env_file" ]] || fail "copy preview/.env.preview.example to preview/.env.preview and edit it"
# Read simple KEY=VALUE entries as data, never execute local env content.
config_value() {
  sed -n "s/^$1=//p" "$env_file" | tail -n 1
}
PREVIEW_LAN_IP="$(config_value PREVIEW_LAN_IP)"
PREVIEW_SQL_PASSWORD="$(config_value PREVIEW_SQL_PASSWORD)"
PREVIEW_IGDB_CLIENT_ID="$(config_value PREVIEW_IGDB_CLIENT_ID)"
PREVIEW_IGDB_CLIENT_SECRET="$(config_value PREVIEW_IGDB_CLIENT_SECRET)"

[[ -n "${PREVIEW_LAN_IP:-}" ]] || fail "PREVIEW_LAN_IP is missing"
[[ "${PREVIEW_LAN_IP}" =~ ^192\.168\.[0-9]{1,3}\.[0-9]{1,3}$|^10\.[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}$|^172\.(1[6-9]|2[0-9]|3[01])\.[0-9]{1,3}\.[0-9]{1,3}$ ]] ||
  fail "preview must bind to a private LAN address, never 0.0.0.0"
[[ "${PREVIEW_SQL_PASSWORD:-}" != *REPLACE* && "${#PREVIEW_SQL_PASSWORD}" -ge 20 ]] ||
  fail "generate a random SQL preview password (20+ chars), not the example value"
[[ -n "${PREVIEW_IGDB_CLIENT_ID:-}" && -n "${PREVIEW_IGDB_CLIENT_SECRET:-}" ]] ||
  fail "set IGDB credentials in the local ignored preview env file"
[[ -f "$secret_file" && -s "$secret_file" ]] ||
  fail "missing real, local, read-only Resend API key file"
IFS= read -r -n 3 first_three < "$secret_file" || true
[[ "$first_three" == "re_" ]] || fail "Resend secret does not look like a key"
[[ -d "$keys_dir" && "$(stat -c '%u' "$keys_dir")" == "1654" ]] ||
  fail "Data Protection keys directory must be owned by UID 1654 (non-root container user)"
if git check-ignore -q "$env_file" && git check-ignore -q "$secret_file" && git check-ignore -q "$keys_dir"; then
  printf 'Preview preflight passed: inputs present, test-only bind and Git ignore verified.\n'
else
  fail "preview credential/key paths must be ignored by Git"
fi
