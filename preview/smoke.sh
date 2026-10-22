#!/usr/bin/env bash
set -euo pipefail
# HTTPS smoke tests: obtain only the PUBLIC local CA cert from preview Caddy,
# verify TLS, then test that all core routes work without credentials.
root="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$root"
env_file="$root/preview/.env.preview"
[[ -f "$env_file" ]] || { echo "Missing preview/.env.preview" >&2; exit 1; }
lan_ip="$(sed -n 's/^PREVIEW_LAN_IP=//p' "$env_file" | tail -n 1)"
[[ -n "$lan_ip" ]] || { echo "Missing PREVIEW_LAN_IP" >&2; exit 1; }
dc() {
  docker compose --project-name gamefilled-v2-preview \
    --env-file "$env_file" -f "$root/compose.preview.yaml" "$@"
}
umask 077
ca="$(mktemp)"
trap 'rm -f "$ca"' EXIT
ready=0
for attempt in 1 2 3 4 5 6 7 8 9 10; do
  if dc exec -T caddy cat /data/caddy/pki/authorities/local/root.crt > "$ca" 2>/dev/null &&
     grep -q 'BEGIN CERTIFICATE' "$ca"; then
    ready=1
    break
  fi
  sleep 2
done
[[ "$ready" == 1 ]] || {
  echo "Preview Caddy CA is not ready. Check preview-only Caddy logs." >&2
  exit 1
}
check() {
  local route="$1" expected="$2" status="" attempt
  for attempt in 1 2 3 4 5 6 7 8; do
    status="$(curl --silent --show-error --noproxy '*' --connect-timeout 5 \
      --max-time 60 --cacert "$ca" \
      --resolve "preview.gamefilled.pt:8444:$lan_ip" \
      -o /dev/null -w '%{http_code}' \
      "https://preview.gamefilled.pt:8444$route")" || status="000"
    if [[ "$status" == "$expected" ]]; then
      printf 'PASS %-21s HTTP %s (verified local TLS)\n' "$route" "$status"
      return 0
    fi
    sleep 2
  done
  printf 'FAIL %-21s expected %s, got %s\n' "$route" "$expected" "$status" >&2
  return 1
}
check /health/live 200
check /health/ready 200
check / 200
check /games 302
check /games/lib/Popular 200
check /people 200
check /users/Login 200
check /users/Register 200
check /css/site.css 200
check /lib/imgs/indexbackground.png 200

index_html="$(curl --silent --show-error --noproxy '*' --max-time 60 --cacert "$ca" \
  --resolve "preview.gamefilled.pt:8444:$lan_ip" \
  "https://preview.gamefilled.pt:8444/")"
if [[ "$index_html" != *'gf-home-hero'* || "$index_html" != *'gf-home-page'* ]]; then
  echo "FAIL original Gamefilled hero/home markup missing; partial backend lab is not an accepted preview." >&2
  exit 1
fi
printf 'PASS original Gamefilled homepage HTML and artwork route verified.\n'
printf 'Preview HTTPS smoke checks passed. Test real user flows in a browser.\n'
