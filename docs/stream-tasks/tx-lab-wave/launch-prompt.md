# Launch — transaction lab

## Mission
A `/lab` admin screen demonstrating the self-contained loop: generate a key in
the browser → address auto-tracked → fund → build+sign a simple P2PKH send
client-side (`dxs-bsv-token-sdk`) → broadcast over the node's own P2P pool →
see the receipt. No third-party provider.

## Package path
`docs/stream-tasks/tx-lab-wave/` — `master.md` (decisions + ledger),
`slices.md` (anchors + tasks).

## Constraints (frozen)
- CLIENT-SIDE keys + signing; the private key NEVER leaves the browser — only
  the signed `rawHex` is POSTed to `/api/tx/broadcast`.
- Reuse existing endpoints (broadcast / utxo / track) — NO backend changes.
- v1 = simple P2PKH send only. Mainnet, clearly labelled as a lab tool with
  real funds.
- Bundle budget must stay green (lazy-load the SDK/route if heavy). Types stay
  generated re-exports.

## Execution
Single slice S1 (`lab-frontend`), frontend-only. Inspect the real
`dxs-bsv-token-sdk` API after install — don't guess method names.

## Validation
`pnpm --dir src/admin-ui verify`; `RAVEN_URL=http://127.0.0.1:18080 pnpm --dir
src/admin-ui test:contract`; `bash scripts/secrets-lint.sh` → 0. Unit-test the
store orchestration (keygen→address, send→rawHex→broadcast, insufficient-funds
error). E2E (operator-run): generate → fund → send → broadcast on mainnet.

## Closeout
Ledger done; hash in master Delivery Notes (separate backfill commit);
`audits/A1.md` + `evidence/closeout.md`; `audits/S1-slice-audit-prompt.md`.
One commit, co-author trailer.
