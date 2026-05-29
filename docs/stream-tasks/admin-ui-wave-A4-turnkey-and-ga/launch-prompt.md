# Launch — Wave A4 (Turnkey self-host + GA validation)

## Mission
Make Consigliere a real turnkey product: a competent operator
pulls the published image, runs one `docker compose` command,
opens `http://localhost:5000`, completes the wizard, and has a
live-indexing node — then prove the production path on real
infrastructure.

## Package path
`docs/stream-tasks/admin-ui-wave-A4-turnkey-and-ga/`

Source of truth: `master.md` (ledger), `slices.md`
(decomposition). Update `master.md` Delivery Notes as slices
close.

Upstream context (read first):
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/
  evidence/closeout.md` — what A3 shipped + its residuals
- `docs/stream-tasks/admin-ui-wave-A3-security-observability/
  audits/A2-hardening.md` — the 4 Low fixes just before this
- `docs/runbook.md` — operator handbook (S2 stopwatch target)
- `compose.yml`, `compose.prod.yml`,
  `src/Dxs.Consigliere/appsettings.{json,DockerComposeE2E.json}`
  — the current first-run reality the wave changes

## Constraints (frozen)
- **Local profile must not weaken prod.** Plain HTTP +
  `cookieSecure=SameAsRequest` are LOCAL-only. `prod` stays
  TLS-always, `cookieSecure=Always`, background tasks on,
  behind Caddy. No shared appsettings edit may downgrade prod.
- **Local front door = plain HTTP on `localhost:5000`.** No
  Caddy in the local overlay; publish `:5000`; zero mandatory
  env vars.
- **Pull-image, not build, for the operator path.**
  `image: dxs/consigliere:${TAG:-latest}`.
- **Live ingest is the bar for S1.** Not a green probe over a
  disabled-task observer.
- **No back-compat shims.** The NRT sweep deletes the
  hand-mirrored interfaces.
- **No secrets in appsettings.** `secrets-lint` stays green.
- **Frontend prod-compile gate is `pnpm verify`** (ends with
  `pnpm contracts:check` — composite `tsc -b` strictness, not
  `tsc --noEmit`). **Backend gate is `dotnet build
  Dxs.Consigliere.sln -c Release`.**
- **Hash-backfill discipline:** Delivery Notes hashes land in a
  SEPARATE commit, never via `--amend`.
- **No fabricated GA evidence.** S2 infra-dependent sub-steps
  are "operator-run, evidence pending" if the VM/domain isn't
  available.

## Execution order
Strictly sequential.

1. **S1 — Local turnkey run mode** — local. The headline gap.
2. **S3 — NRT hand-mirrored-interface sweep** — local;
   pure-code, no infra; different ownership zone from S1.
3. **S2 — Real prod bring-up + soak + runbook stopwatch** —
   needs real VM + domain; gates GA sign-off.

Use `/execution-operator` semantics: one ledger, bounded
subagents via the `Agent` tool (max 3 parallel), close
completed background agents promptly with `TaskStop`. Slices
are sequential here; parallel fan-out only INSIDE a slice.

Per-slice cadence (the wave-A3 pattern):
1. Read the slice section in `slices.md` end-to-end.
2. Implement; run local validation per slice.
3. Draft an audit prompt at `audits/S<n>-slice-audit-prompt.md`.
4. Codex slice audit runs externally → record findings in
   `audits/S<n>-slice-audit.md`.
5. Fold findings; record in `audits/S<n>-slice-audit-followup.md`.
6. Commit `feat|fix(<area>): wave-A4 S<n> — <summary>`; backfill
   the Delivery Notes hash in a separate `docs(...)` commit.

## Validation
- **S1**: clean checkout → `docker compose -f compose.yml -f
  compose.local.yml up -d` → `http://localhost:5000` wizard
  completes with no cert warning → watched address indexes with
  a real JungleBus sub id; `docker compose -f compose.yml -f
  compose.prod.yml --profile prod config` still TLS-always +
  `cookieSecure=Always`; `secrets-lint` green.
- **S3**: `grep -rn "interface Admin\|interface P2p\|interface
  Source\|interface Setup" src/admin-ui/src/types/{admin,auth}.ts`
  == 0; `pnpm verify` green; `pnpm test:contract` 24/24.
- **S2**: real Let's Encrypt cert chain captured; ≥24h soak
  (stable memory + continuous ingest); non-author runbook
  stopwatch <45 min — or honest "operator-run, evidence
  pending".

End-to-end: a stranger pulls `dxs/consigliere:latest`, runs the
local compose, and is watching a live-indexing address through
the wizard with no domain; separately, a real operator stands
the prod stack up from the runbook alone.

## Closeout
- S1 + S3 `done`; S2 `done` or its infra sub-steps "operator-run,
  evidence pending".
- Commit hashes in `master.md` Delivery Notes (separate commits).
- `audits/A1.md` (what landed, validation, residuals).
- `evidence/closeout.md` (result, key files, before/after
  install story, honest residuals + GA evidence or pending
  markers).

## Commit / report expectations
- One commit per completed slice; one per audit-fold pass.
- Co-authored trailer:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- Final report (chat, at closeout): result first, zones done,
  validation runs, real residuals.
