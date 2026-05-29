#!/usr/bin/env bash
# wave-A4 S2 — soak watcher for the GA-validation pass.
#
# Polls the running Consigliere host's readiness + container
# memory at a fixed interval and appends a CSV line per sample,
# so a >=24h soak produces a reviewable trace of:
#   - readiness stability (no flapping)
#   - memory trend (no unbounded growth = no leak)
#   - ingest liveness (block tip advancing)
#
# Usage:
#   BASE_URL=https://<domain> CONTAINER=consigliere-app \
#     bash scripts/soak-watch.sh out.csv [interval_seconds]
#
# Defaults: BASE_URL=http://localhost:5000, CONTAINER=consigliere-app,
# interval=300s. Run it in tmux/nohup for the full window; review
# out.csv at the end (memory column should plateau, ready should
# stay 200, tip_height should advance).

set -uo pipefail

OUT="${1:?usage: soak-watch.sh <out.csv> [interval_seconds]}"
INTERVAL="${2:-300}"
BASE_URL="${BASE_URL:-http://localhost:5000}"
CONTAINER="${CONTAINER:-consigliere-app}"

if [[ ! -f "$OUT" ]]; then
  echo "ts_utc,ready_code,ready_status,mem_bytes,tip_height" > "$OUT"
fi

read_tip() {
  # Best-effort: the headers tip endpoint is admin-gated, so a
  # bare curl returns 401 unless a cookie is supplied. If you
  # want tip tracking, export COOKIE="consigliere_admin=..." and
  # this picks it up. Otherwise tip_height stays empty.
  if [[ -n "${COOKIE:-}" ]]; then
    curl -ksf -H "Cookie: ${COOKIE}" "${BASE_URL}/api/admin/p2p/headers/tip" 2>/dev/null \
      | grep -oE '"height"[: ]*[0-9]+' | grep -oE '[0-9]+' | head -1
  fi
}

while true; do
  ts=$(date -u +%Y-%m-%dT%H:%M:%SZ)
  body=$(curl -ksf -o /dev/null -w "%{http_code}" "${BASE_URL}/health/ready" 2>/dev/null || echo "000")
  status=$(curl -ksf "${BASE_URL}/health/ready" 2>/dev/null | grep -oE '"status"[: ]*"[a-z]+"' | grep -oE '[a-z]+$' | head -1)
  mem=$(docker stats --no-stream --format '{{.MemUsage}}' "$CONTAINER" 2>/dev/null | awk '{print $1}')
  tip=$(read_tip)
  echo "${ts},${body},${status:-},${mem:-},${tip:-}" >> "$OUT"
  sleep "$INTERVAL"
done
