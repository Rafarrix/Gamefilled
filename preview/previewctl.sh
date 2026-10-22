#!/usr/bin/env bash
set -euo pipefail

# Controls ONLY the dedicated V2 preview Compose project.
# Run manually from a separate checkout on the Debian VM.
root="$(cd "$(dirname "$0")/.." && pwd -P)"
cd "$root"
project="gamefilled-v2-preview"
preview_env="$root/preview/.env.preview"

die() { printf 'Preview: %s\n' "$1" >&2; exit 1; }
note() { printf 'Preview: %s\n' "$1"; }
dc() {
  docker compose --project-name "$project" --env-file "$preview_env" \
    -f "$root/compose.preview.yaml" "$@"
}
value_from_env() {
  sed -n "s/^$1=//p" "$preview_env" | tail -n 1
}
ensure_checkout() {
  # Never operate from the current live Compose checkout, even by accident.
  case "$root/" in
    /opt/gamefilled/|/opt/gamefilled/*) die "refusing live /opt/gamefilled checkout" ;;
  esac
  command -v git >/dev/null && command -v docker >/dev/null ||
    die "Git and Docker are required"
  [[ -f "$preview_env" ]] ||
    die "local preview/.env.preview is missing; follow preview/README.md"
  [[ "$(git rev-parse --show-toplevel 2>/dev/null)" == "$root" ]] ||
    die "run from a dedicated full Git checkout"
}
verify_host() {
  ensure_checkout
  [[ "$(git branch --show-current)" == "master" ]] ||
    die "preview deploys only reviewed master commits"
  [[ -z "$(git status --porcelain --untracked-files=no)" ]] ||
    die "tracked worktree changes detected; use a clean checkout"
  bash preview/preflight.sh
  command -v ip >/dev/null || die "iproute2 is required for LAN-IP validation"
  command -v curl >/dev/null || die "curl is required for health checks"
  lan_ip="$(value_from_env PREVIEW_LAN_IP)"
  ip -4 -o addr show | awk '{print $4}' | cut -d/ -f1 | grep -Fxq -- "$lan_ip" ||
    die "PREVIEW_LAN_IP is not assigned to this VM (check ip -4 -br addr)"
  local free_mem_kib free_disk_kib
  free_mem_kib="$(awk '/^MemAvailable:/ {print $2}' /proc/meminfo)"
  free_disk_kib="$(df -Pk "$root" | awk 'NR==2 {print $4}')"
  # Initial deployment needs headroom for SQL + first image builds. On subsequent
  # updates the already-running preview SQL uses some of that memory; require
  # 3 GiB remaining for the image build instead of incorrectly demanding 5 GiB.
  if [[ -n "$(dc ps -q sql)" ]]; then
    [[ "${free_mem_kib:-0}" -ge 3145728 ]] ||
      die "less than 3 GiB RAM available for a running-preview update; increase VM resources before building"
  else
    [[ "${free_mem_kib:-0}" -ge 5242880 ]] ||
      die "less than 5 GiB RAM available for a first preview deployment; review VM resources"
  fi
  [[ "${free_disk_kib:-0}" -ge 10485760 ]] ||
    die "less than 10 GiB free on checkout filesystem; review storage first"
  dc config --quiet
  if [[ -z "$(dc ps -q caddy)" ]] && command -v ss >/dev/null &&
     [[ -n "$(ss -H -lnt '( sport = :8444 )')" ]]; then
    die "TCP port 8444 already has a listener; identify it before deploying"
  fi
  note "Preflight, local bind, resources and Compose configuration passed."
  note "Check separately for Docker/VPN network overlap with 172.28.81.0/24."
}
confirm_deploy() {
  local expected
  if [[ -n "$(dc ps -q sql)" ]]; then
    expected="UPDATE PREVIEW"
    note "Existing preview SQL: create a preview database backup and review EF migrations."
  else
    expected="START PREVIEW"
    note "A second SQL Server may increase VM RAM/CPU/disk consumption."
  fi
  note "This is preview-only. Type exactly: $expected"
  local typed
  IFS= read -r typed
  [[ "$typed" == "$expected" ]] || die "not confirmed; no deployment performed"
}
deploy() {
  verify_host
  confirm_deploy
  export PREVIEW_REVISION
  PREVIEW_REVISION="$(git rev-parse --short=12 HEAD)"
  note "Building reviewed revision $PREVIEW_REVISION."
  dc build web migrator
  dc up -d sql
  note "Applying migrations ONLY to GamefilledV2Preview."
  local ok=0
  for attempt in 1 2 3 4 5 6; do
    if dc --profile setup run --rm migrator; then ok=1; break; fi
    note "Preview migration attempt $attempt failed; retrying as SQL initializes."
    sleep 10
  done
  [[ "$ok" == 1 ]] || die "preview migration failed; web was not replaced"
  dc up -d web caddy
  note "Preview started; checking HTTPS endpoints."
  bash preview/smoke.sh
}
status() {
  ensure_checkout
  note "Local Git: $(git rev-parse --short=12 HEAD)"
  dc ps
  local revision
  revision="$(dc exec -T web printenv Preview__Revision 2>/dev/null || true)"
  [[ -n "$revision" ]] && note "Deployed preview image: $revision" ||
    note "Preview web image not running (or revision unavailable)."
}
stop_preview() {
  ensure_checkout
  note "Stopping ONLY preview containers; preserving volumes."
  dc stop
}
case "${1:-}" in
  check) verify_host ;;
  deploy) deploy ;;
  status) status ;;
  smoke) ensure_checkout; bash preview/smoke.sh ;;
  stop) stop_preview ;;
  *)
    printf 'Usage: bash preview/previewctl.sh {check|deploy|status|smoke|stop}\n'
    exit 2 ;;
esac
