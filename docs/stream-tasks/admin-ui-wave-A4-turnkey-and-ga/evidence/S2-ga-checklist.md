# S2 — GA validation checklist (operator-run)

The one slice engineering cannot fully self-certify: it needs a
real VM, a real public DNS name, and 24 hours of wall-clock. This
file is the exact runbook for the operator who has those, plus
the pass criteria and where to drop evidence. Sub-steps that
require real infra are marked **[operator-run, evidence pending]**
until executed.

> Status at wave close: **evidence pending** — not run in the
> build environment (no public VM/domain). Everything code-side
> that gates these steps is green (see `S1-local-smoke.md`,
> backend Release build, `pnpm verify`, `pnpm test:contract`
> 24/24, secrets-lint 0).

## 0. Cut the first release  **[maintainer action]**
The turnkey "pull our image" path (S1) is gated on a published
image. None exists yet — the DockerHub workflow fires only on a
`vX.Y.Z` tag (README § "Docker Release Policy").

```bash
git checkout main && git pull --ff-only
git tag v0.1.0          # or the chosen first stable version
git push origin v0.1.0  # → CI publishes dxs/consigliere:{0.1.0,0.1,0,latest}
```
Pass: `docker pull dxs/consigliere:latest` succeeds from a clean
host. Capture the digest.

## 1. Prod bring-up on a real VM  **[operator-run, evidence pending]**
Prereqs: a VM (4 vCPU / 8 GB / 100 GB per the runbook), inbound
80+443 open, a DNS A/AAAA record for `<domain>` already resolving
to the VM, a free JungleBus subscription id.

```bash
git clone https://github.com/dxsapp/dxs-consigliere.git && cd dxs-consigliere
printf 'CADDY_DOMAIN=<domain>\nCADDY_EMAIL=<ops@you>\nBSV_NODE_RPC_PASSWORD=%s\n' "$(openssl rand -hex 24)" > .env && chmod 600 .env
docker compose -f compose.yml -f compose.prod.yml --profile prod up -d --build
docker logs -f consigliere-caddy-prod 2>&1 | grep -E "certificate obtained|error"
```
Pass + capture:
- `curl -I https://<domain>/` → `200` + `strict-transport-security` header → save to `evidence/prod-headers.txt`.
- real Let's Encrypt chain: `openssl s_client -connect <domain>:443 -servername <domain> </dev/null 2>/dev/null | openssl x509 -noout -issuer -subject -dates` → save to `evidence/prod-cert.txt` (issuer must be Let's Encrypt, NOT Caddy Local).
- complete the wizard over `https://<domain>/setup`; sign in.
- broadcast a real tx (or force-rebroadcast from the queue); confirm it reaches peers (the broadcast receipt advances past Validated) → screenshot to `evidence/`.

## 2. >=24h soak  **[operator-run, evidence pending]**
```bash
# on the VM, in tmux/nohup, with an admin cookie for tip tracking:
BASE_URL=https://<domain> CONTAINER=consigliere-app \
  COOKIE="consigliere_admin=<paste from browser>" \
  bash scripts/soak-watch.sh evidence/soak.csv 300
```
Pass criteria over the window (review `evidence/soak.csv`):
- `ready_code` stays `200` (no flapping to 503).
- `mem_bytes` plateaus — no unbounded growth (= no leak).
- `tip_height` advances (ingest stays live).
- audit-log `@expires` bundle is enabled: in Raven Studio confirm
  `AuditLogEntries` documents carry an `@expires` metadata header
  ~365d out.

## 3. Runbook stopwatch  **[operator-run, evidence pending]**
Hand `docs/runbook.md` to someone who has NOT seen the codebase.
They stand up the prod stack reading ONLY the runbook. Time it.
- Pass: working TLS-fronted, wizard-completed Consigliere in
  **< 45 min**.
- Every step that was ambiguous / broke → fix `docs/runbook.md`
  and note the fix here.
- Record: `evidence/stopwatch.md` — who, start/stop time, total,
  any runbook corrections made.

## Honest closeout rule
Do NOT mark S2 `done` on the strength of this checklist alone.
Each [operator-run] sub-step is `done` only when its evidence
file exists. Until then the wave closeout records S2 as
"prepared; operator-run sub-steps evidence-pending" — never
fabricated.
