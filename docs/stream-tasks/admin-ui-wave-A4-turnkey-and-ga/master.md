---
created: 2026-05-21
type: wave
status: approved (planning only — no slices executed yet)
parent: docs/stream-tasks/admin-ui-wave-A3-security-observability/
related: docs/stream-tasks/admin-ui-wave-A3-security-observability/evidence/closeout.md (wave-A3 closed at HEAD 41942b3);
         docs/stream-tasks/admin-ui-wave-A3-security-observability/audits/A2-hardening.md (the 4 Low fixes that preceded this wave);
         docs/runbook.md (operator handbook S7 shipped)
---

# Admin UI Wave A4 — Turnkey self-host + GA validation

Wave-A3 made Consigliere production-*capable* (TLS, rate
limiting, health probes, audit log, live logs, secrets at rest,
operator runbook). It did **not** make it turnkey-*installable*,
and it never ran on real infrastructure.

Wave-A4 closes both gaps so the target actor — a competent
operator/agent who can get a free JungleBus subscription in a
minute — can: **pull the published image → `docker compose up`
→ open `http://localhost:5000` → complete the wizard → have a
live, indexing product**, with no domain, no ACME, no manual
config, no curl.

## Goal

A first-time operator on their own machine reaches a working,
live-ingesting Consigliere through the browser wizard alone —
and a separate GA-validation pass proves the prod path works on
real infrastructure (real cert, real peers, 24h soak, a
stranger stands it up from the runbook in <45 min).

Business outcome:
- The "download → run → use" story is real, not two disjoint
  half-paths.
- GA sign-off is backed by a real prod bring-up + soak, not
  only unit/contract tests + local dev smoke.
- The wave-A3 S6 NRT residual is finished, so the admin UI
  consumes generated types end-to-end.

## Product Decision

- **There is a dedicated LOCAL run mode, separate from both the
  E2E-smoke profile and the public-prod profile.** Today
  `docker compose up` defaults to the `DockerComposeE2E` env
  which sets `BackgroundTasks.DisableAll: true` +
  `ScanMempoolOnStart: false` — the README itself says it is
  "not for live chain ingest". The only live-ingest path is the
  `prod` profile, which mandates `CADDY_DOMAIN` + `CADDY_EMAIL`
  + real DNS + ACME. Neither serves "run it on my laptop and
  use it". S1 adds that third mode.
- **Local front door = plain HTTP on `localhost:5000`.**
  Operator decision (this wave). No Caddy in the local profile;
  the `consigliere` service publishes `:5000` to the host; the
  admin cookie uses `cookieSecure = SameAsRequest` so it sets
  over plain HTTP. Maximally smooth — no self-signed cert
  warning, no CA import, no domain. TLS is a *production*
  concern and stays exactly as wave-A3 S0 shipped it; the local
  profile must NOT weaken the prod posture.
- **Local mode pulls the published image, not `build:`.**
  `image: dxs/consigliere:${TAG:-latest}` (overridable),
  matching the README's "pull our image" promise. The existing
  `compose.yml` `build:` path stays for contributors.
- **Local mode runs the real working config.** Background tasks
  ON, mempool scan ON — i.e. the base `appsettings.json` shape
  (Mainnet, the 8 EnabledTasks), NOT `DockerComposeE2E`, NOT
  `prod`-with-`${ENV}`-placeholders. RavenDb URL pointed at the
  in-compose `ravendb:8080`.
- **GA validation is a first-class slice, not a checkbox.** The
  prod bring-up + 24h soak + runbook stopwatch is the one thing
  engineering cannot self-certify; S2 preps the harness +
  checklist and captures real evidence. Sub-steps needing real
  infra are honestly marked "operator-run, evidence pending" if
  the VM/domain isn't available at execution time.

## Scope

In scope:
- `compose.local.yml` overlay (image-based, plain-HTTP, real
  config, zero mandatory env) + a new `appsettings.Local.json`
  (or a documented reuse of base) wired via the local env.
- Verify (and fix or honestly document) that completing the
  wizard starts live ingest **without a host restart**.
- README rewrite: a real "Run locally" quickstart replacing the
  "E2E smoke / not for ingest" framing.
- Real prod bring-up on a VM + ≥24h soak + runbook stopwatch;
  evidence captured under `evidence/`.
- NRT hand-mirrored-interface sweep finishing wave-A3 S6: the
  remaining ~30 `Dto.cs` response files migrated to
  `#nullable enable`, regenerated `api.generated.ts`, and
  `types/{admin,auth}.ts` reduced to generated re-exports.

Out of scope (with rationale — do NOT pull these in):
- **Multi-instance HA** — rate-limit counters, the log-stream
  ring, and the audit retention bundle are per-process by
  design. Cluster-awareness is a multi-week wave of its own
  (A5). Bundling it with a day-scale local-run fix would be
  lopsided.
- **RBAC / multi-user accounts** — A5+.
- **OpenTelemetry distributed tracing** — later wave.
- **Raven encryption-at-rest** — compliance wave.

## Core Rules

1. **The local profile must not weaken the prod posture.**
   Plain HTTP + `cookieSecure=SameAsRequest` apply to the LOCAL
   profile ONLY. `prod` stays TLS-always,
   `cookieSecure=Always`, background tasks on, behind Caddy.
   No shared appsettings change may downgrade prod.
2. **No back-compat shims.** Both ends of every consumer chain
   are in-repo. The NRT sweep DELETES the hand-mirrored
   interfaces; it does not keep them beside the re-exports.
3. **Pull-image, not build, for the operator path.** The local
   overlay references `dxs/consigliere:${TAG:-latest}`. If a
   contributor wants a local build they use the existing
   `compose.yml` `build:` service.
4. **Live ingest is the local mode's definition of working.**
   "Wizard completes + I can watch an address + it indexes" is
   the bar. A green health probe over a dead observer does NOT
   satisfy S1.
5. **GA validation evidence is real or honestly absent.** S2
   captures real cert chains / soak metrics / stopwatch timing,
   OR marks the infra-dependent sub-steps "operator-run,
   evidence pending" — never fabricated.
6. **Hash-backfill discipline (carried from A3).** Record
   Delivery Notes commit hashes in a SEPARATE follow-up commit,
   never via `git commit --amend`.

## Ownership Zones

- `compose` / deploy — `compose.local.yml`, `compose.yml`
  (touch only the local service), `Dockerfile` if needed,
  `src/Dxs.Consigliere/appsettings.Local.json` (new),
  `README.md` quickstart. (S1)
- `ops-validation` — `evidence/` (cert chain, soak metrics,
  stopwatch), prep checklist, any soak-watch script. (S2)
- `contract-types` — `src/Dxs.Consigliere/**/Dto/**/*.cs`
  (`#nullable enable` migrations), `src/admin-ui/contracts/
  swagger.json`, `src/admin-ui/src/types/{admin,auth}.ts`,
  `api.generated.ts`. (S3)

## Wave Ledger

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S1 | `compose`/deploy | **done** | — | From a clean checkout: `docker compose -f compose.yml -f compose.local.yml up -d` (or chosen invocation) → `http://localhost:5000` → wizard completes (no domain, no cert warning) → a watched address indexes with a real JungleBus sub id; `docker compose --profile prod config` still materialises TLS-always + `cookieSecure=Always` (prod posture intact) | Local mode pulls `dxs/consigliere:${TAG:-latest}`, runs real-ingest config, serves plain HTTP on `:5000`, needs ZERO mandatory env; wizard→live-ingest confirmed restart-free (or restart documented); README "Run locally" quickstart replaces the E2E-smoke framing | `audits/S1-slice-audit-prompt.md` |
| S3 | `contract-types` | todo | S1 (none hard; ordered before S2 because pure-code) | `grep -rn "interface Admin\|interface P2p\|interface Source\|interface Setup" src/admin-ui/src/types/{admin,auth}.ts` returns ZERO matches; `pnpm verify` green at each step; `pnpm test:contract` still 24/24 | Remaining ~30 response `Dto.cs` files `#nullable enable`d + NotNull props initialised; swagger + `api.generated.ts` regenerated with populated `required`; `types/{admin,auth}.ts` are pure generated re-exports | `audits/S3-slice-audit-prompt.md` |
| S2 | `ops-validation` | todo | S1 (local mode lets a reviewer rehearse the flow first) | Real VM + real domain: Let's Encrypt cert issues (chain captured), wizard completes, broadcast reaches real peers; ≥24h soak shows no task leak / ingest stays live / audit `@expires` holds; a non-author stands the stack up from ONLY `docs/runbook.md` in <45 min (timed) | GA sign-off evidence in `evidence/` OR infra-dependent sub-steps explicitly marked "operator-run, evidence pending" — never fabricated | `audits/S2-slice-audit-prompt.md` |

## Definition of Done

- S1 + S3 `done`; S2 `done` OR its infra-dependent sub-steps
  honestly marked "operator-run, evidence pending".
- A non-contributor can go pull-image → compose up →
  `http://localhost:5000` → wizard → watching a live-indexing
  address, with no domain and no manual config.
- `pnpm verify` + `pnpm test:contract` (24/24) + `dotnet build
  Dxs.Consigliere.sln -c Release` + `bash scripts/secrets-lint.sh`
  all green.
- Prod posture unchanged: `docker compose -f compose.yml -f
  compose.prod.yml --profile prod config` still shows
  `ASPNETCORE_ENVIRONMENT=Production`, TLS-always,
  `cookieSecure=Always`.
- `audits/A1.md` + `evidence/closeout.md` written per playbook.

## Delivery Notes

Per-slice commit hashes recorded here at closeout (separate
backfill commit, never `--amend`):

| slice | commit | summary |
|---|---|---|
| S1 | `fd67479` | local turnkey run mode (image + real config + plain-HTTP localhost) + README quickstart_ |
| S3 | _pending_ | _NRT hand-mirrored-interface sweep → generated re-exports_ |
| S2 | _pending_ | _real prod bring-up + 24h soak + runbook stopwatch evidence_ |
| Audit folds | _pending_ | _per-slice findings folded_ |
