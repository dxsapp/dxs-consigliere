#!/usr/bin/env bash
# wave-A3 S5 — grep gate that fails CI if any
# `src/Dxs.Consigliere/appsettings*.json` file reintroduces a
# plaintext API key / password / secret / token. Values of the
# form `"${ENV}"`, `""`, or comment-style strings are skipped;
# anything else under those well-known key names is treated as
# a regression.
#
# Run locally:  bash scripts/secrets-lint.sh
#
# Exit code: 0 on clean tree, 1 on any plaintext hit.

set -euo pipefail

ROOT=$(cd "$(dirname "$0")/.." && pwd)
shopt -s nullglob
files=(
  "$ROOT"/src/Dxs.Consigliere/appsettings*.json
)
if [[ ${#files[@]} -eq 0 ]]; then
  echo "[secrets-lint] no appsettings*.json found under src/Dxs.Consigliere/"
  exit 0
fi

# Match keys like apiKey / Password / Secret / Token whose value
# starts with a character that ISN'T:
#   - a quote (i.e. the value is the empty string `""`)
#   - a `$` (i.e. an env-var placeholder `"${VAR}"`)
#   - a `{` (i.e. an interpolation token)
pattern='"([Aa]pi[Kk]ey|[Pp]assword|[Ss]ecret|[Tt]oken)"[[:space:]]*:[[:space:]]*"[^"${]'

if grep -nE "$pattern" "${files[@]}"; then
  echo
  echo "[secrets-lint] FAIL — plaintext secret-like value detected above."
  echo "[secrets-lint] Move it onto the SecretsFileStore (data/secrets/providers.json)"
  echo "[secrets-lint] or substitute it via env var (\"\${MY_API_KEY}\")."
  exit 1
fi

echo "[secrets-lint] OK — no plaintext secrets in appsettings files."
