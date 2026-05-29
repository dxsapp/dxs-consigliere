# Launch — thin-node primary source (routed realtime + rawTx)

## Mission

Make the BSV thin node a first-class routable provider `p2p` — the
default primary for realtime + rawTx — with external providers demoted
to auto-fallback, JungleBus scoped to historical block backfill, all
realtime sources running concurrently with per-source first-seen
latency, and the first-run wizard remodeled to match.

## Package path

`docs/stream-tasks/thin-node-primary-source-wave/`

Source of truth: `master.md` (ledger + product decisions), `slices.md`
(decomposition + verified anchors). Update `master.md` as slices close.
Parent program: `docs/stream-tasks/consigliere-thin-node-observer-program/`.

## Constraints (frozen)

- Provider name is `p2p` (distinct from the RPC `node` provider). Matches
  the existing `TxObservationSource.P2p` / `SeenBySources` tag.
- rawTx P2P miss returns **null**, never throws — the existing
  `RawTransactionFetchService` loop must reach external fallback.
- Making `p2p` the realtime primary must NOT silence Bitails/JungleBus
  realtime runners — all configured realtime sources run concurrently
  (operator decision: redundant + measure who's fastest, NOT failover).
- Cold-start honest: with `p2p` primary and 0 connected peers, the
  external runners must still feed the journal.
- `p2p` is descriptor-registered in the catalog (an
  `IExternalChainProviderDiagnostics`), not hard-coded; the only
  special-case is the `BsvP2pConfig.Enabled` routability gate.
- No back-compat shims; no hand-mirrored DTOs (wave-A4 S3 invariant —
  `types/{admin,auth}.ts` stay generated re-exports).
- Out of scope: inbound listener, P2P block backfill, P2P historical tx
  archive, removing the `node` provider.

## Execution order

Use `/execution-operator` semantics: one ledger, bounded subagents via
`Agent` (max 3 parallel), close completed background agents promptly
(`TaskStop`).

1. **S1** — `provider-routing`: register `p2p`, defaults. (local or one
   agent) — MUST close before S2/S5.
2. **S2** — `provider-routing`: P2P rawTx fetch + fallback. After S1.
3. **S3** — `realtime-orchestration`: all realtime concurrent. After S1;
   parallel with S2 (disjoint zone).
4. **S4** — `source-metrics`: first-seen latency. After S3.
5. **S5** — `setup-wizard`: wizard remodel. After S1; validate last
   (regenerates contracts).

On ambiguity: smallest safe judgment call, document in `audits/A1.md`,
continue. Do NOT halt unless a truly blocking contradiction emerges.

## Validation

Per slice — see `slices.md` validation matrix. Gates:
- Backend: `dotnet build Dxs.Consigliere.sln -c Release`.
- Frontend (S4, S5): `pnpm verify` (ends with `contracts:check`) +
  `RAVEN_URL=http://127.0.0.1:18080 pnpm --dir src/admin-ui test:contract`
  (24/24).
- `bash scripts/secrets-lint.sh` → 0.
- End-to-end: fresh turnkey local stack
  (`docker compose -f compose.local.yml -f compose.local-build.yml up -d
  --build`) → wizard shows p2p default → watched address indexes; rawtx
  mempool hit via p2p, confirmed-tx auto-fallback to external; metrics
  screen shows per-source first-seen wins.

## Closeout

- All ledger rows `done` or `not_opened`.
- Commit hashes in `master.md` Delivery Notes (SEPARATE backfill commit,
  never `--amend`).
- `audits/A1.md` (what landed, validated, residuals — call out the
  cold-start P2P warmup window honestly).
- `evidence/closeout.md` (result, key files, behavioural summary).
- Per-slice `audits/S<n>-slice-audit-prompt.md` for the codex audit
  cadence (same as A3/A4).

## Commit / report expectations

- One commit per completed slice; scoped to its ownership zone.
- Co-author trailer on every commit:
  `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.
- Final report: result first, zones done, validation run, real residuals.
