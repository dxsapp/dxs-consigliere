# Launch — wizard-enabled P2P + runtime toggle

## Mission
Completing the first-run wizard turns the BSV thin node ON, live, with no
restart — by persisting `P2pEnabled` to a DB-authoritative-after-seed
runtime-settings document and making `BsvP2pHostedService` a supervisor
that starts/stops the peer pool on that flag via the RavenDB Changes API.

## Package path
`docs/stream-tasks/wizard-enabled-p2p-runtime-toggle-wave/`
Source of truth: `master.md` (ledger + decisions), `slices.md` (anchors +
decomposition). Update `master.md` as slices close.
Parent program: `docs/stream-tasks/consigliere-thin-node-observer-program/`.

## Constraints (frozen)
- DB authoritative AFTER seed; config (`BsvP2pConfig.Enabled`, default
  false) is the first-run default and the CI/E2E signal. Read path:
  `effectiveEnabled = dbOverride ?? configSeed`.
- Restart-free: flipping the flag must start/stop the pool at runtime.
- Start/stop idempotent + clean (no leaked sessions/dispatchers/timers);
  routing/rawTx/realtime/broadcast must never throw on a mid-run toggle.
- CI/E2E (no wizard, no DB override) stay OFF — must not connect to peers.
- No secrets in the new document; secrets stay in the secrets file. Do NOT
  enable P2P in base appsettings. Do NOT add an admin toggle endpoint (out
  of scope). Do NOT migrate other settings to the DB.

## Execution order
1. S1 `runtime-settings` — OperatorRuntimeSettingsDocument + seed-from-config
   service + wizard writes `P2pEnabled=true`.
2. S2 `p2p-lifecycle` — BsvP2pHostedService supervisor + Changes-API live
   toggle (depends on S1).
Sequential (small, same backend). `/execution-operator` semantics; close
background agents promptly. On ambiguity: smallest safe call, document in
A1, continue.

## Validation
- `dotnet build Dxs.Consigliere.sln -c Release` clean.
- S1 unit: seed/override round-trip + wizard write. S2 unit/integration:
  supervisor start/stop + idempotency + CI-safe-off (no peer connect when
  config seed false + no override).
- `bash scripts/secrets-lint.sh` → 0.
- Pre-existing `RavenTestDriver`/production-DI env test failures are not in
  scope.
- E2E (operator-run, evidence-pending): fresh Local stack, P2P off →
  complete wizard → `BsvP2pHealth.Bound` true, peers connecting, no restart.

## Closeout
- Ledger rows done / not_opened; commit hashes in `master.md` Delivery
  Notes (SEPARATE backfill commit, never `--amend`).
- `audits/A1.md` (what landed, validated, residuals — esp. live E2E
  evidence-pending + idempotency/teardown notes).
- `evidence/closeout.md`.
- Per-slice `audits/S<n>-slice-audit-prompt.md` for the codex cadence.

## Commit / report expectations
- One commit per slice, scoped to its zone. Co-author trailer on each.
- Final report: result first, zones done, validation run, real residuals.
