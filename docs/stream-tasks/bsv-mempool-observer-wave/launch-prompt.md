# Launch — Wave 2: BSV Mempool Observer

## Mission

Make the BSV P2P pool a first-class transaction-observation source
inside Consigliere. Every `inv(MSG_TX)` from a connected peer ends
up rate-limit-filtered, fetched via `getdata`, parsed,
matched against a hot-reloaded watchlist, and appended to the
existing `TxObservationJournalWriter` with `SeenBySources += "p2p"`.
Bitails and JungleBus realtime runners stay alive with their
source tags explicit so W4 can later measure each source's
contribution.

End state at wave close:

- `TxObservationSource.P2p` constant in
  `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs`.
- Source-neutral `TxObservationJournalWriter.AppendAsync(observation,
  payload, source, ct)` overload landed.
- `WatchlistMatcher` (HashSet<ulong> 8-byte prefix + full hash160
  verify) live with `RavenWatchlistLoader` hot reload via the
  **Raven Changes API** (matches the
  `StasAttributesChangeObserverTask` pattern; Subscription API is
  NOT used — see slices.md §S3). 500 K addresses load ≤ 2 s;
  hot-path lookup p99 ≤ 100 ns.
- `MempoolWatcher` + `P2pMempoolIngestRunner` hosted service wired
  in DI, consuming the frozen Wave 1 S0 callbacks; tx-frame await
  goes through `PerSessionFrameDispatcher` (no parallel reader on
  `IncomingMessages`).
- Bitails / JungleBus runner production code **unchanged**. S6 is
  a regression-test pin only — both runners already tag observations
  correctly via `TxMessage.Source`.
- Watchlist correctness fixture suite green (address-out, address-in,
  STAS / DSTAS token out, delete-during-observation, 8-byte prefix
  collision).
- One live mainnet hit recorded in `evidence/live-validation.md`
  (operator-driven; may defer with explicit reason).

## Package path

`docs/stream-tasks/bsv-mempool-observer-wave/`

Sources of truth:

- `master.md` — wave-level scope, rules, ownership, handoff
  table, slice ledger.
- `slices.md` — per-slice decomposition + dependency graph +
  validation matrix.

Parent program: `docs/stream-tasks/consigliere-thin-node-observer-program/`.

## Prerequisite-slice gating (mandatory)

This wave has a **prerequisite slice** per the program launch
prompt:

- **S0 — Journal contract extension** has `depends_on = —` (none).
- **S1-S8** all have `depends_on` that includes S0 explicitly
  (see `slices.md` §Dependency Graph).

S0 lands first, on its own, with a slice-level audit at
`audits/S0-A1.md`. **No main slice (S1-S8) opens until S0's slice
audit returns APPROVE.** Mirrors the W1 pattern.

## Wave-wide prerequisite (Wave 1 closed)

Every slice in this wave assumes the frozen Wave 1 S0 surface:

- `PeerSession.OnInvReceived(InvMessage)`,
  `PeerSession.OnRejectReceived(RejectMessage)`,
  `PeerSession.OnHeadersReceived` callbacks
- `PeerSession.SendGetHeadersAsync`,
  `PeerSession.SendGetDataAsync`,
  `PeerSession.SendTxAsync` send helpers
- `IPeerTelemetrySink` rich-event interface +
  `PeerTelemetry` snapshot
- `IBlockHeaderStore` / `HeadersChain` for confirmation lookups
  (S0 of W2 doesn't need these directly, but downstream may)

The program ledger records Wave 1 closed 2026-05-17 with
A2-followup-2 APPROVE.

## Constraints (frozen)

- **No new hub events, server methods, or `PeerSession` callbacks.**
  All needed surface is frozen by Wave 1 S0. If a slice surfaces a
  need S0 missed, open a contract-freeze amendment slice in
  `docs/stream-tasks/bsv-headers-chain-wave/` first.
- **`PeerSession.IncomingMessages` is single-consumer.** Audit W2
  H1: `ChannelReader<T>` delivers each frame to exactly one
  consumer; `TxRelayCoordinator` (Gate 3) already reads it per
  session. W2's mempool runner must NOT add a parallel reader.
  S5 introduces `PerSessionFrameDispatcher` that owns the single
  reader and fans frames to multiple subscribers (relay coordinator +
  mempool watcher). `TxRelayCoordinator` is refactored in S5 to
  consume via the dispatcher — behavioural parity. Adding
  `OnTxReceived` to `PeerSession` is forbidden (would violate the
  W1 S0 contract freeze).
- **HashSet<ulong> watchlist, not bloom filter** (program rule).
  8-byte prefix index + full hash160 verify on hit.
- **No reorg recovery in W2.** That's W3. Even if we observe a tx
  that later gets reorged, W2 only records the observation; W3
  owns the state transition.
- **Bitails / JungleBus runners stay alive.** S6 cleanup is
  source-tag only — no behavioural change to those runners.
- **No new public REST/SignalR surface.** Existing
  `OnTransactionFound` continues to fire on watchlist hits with
  unchanged payload.
- **Stop-and-audit per slice on S0, per wave on S1-S8.**
- **Tests use [SkippableFact]** for Raven-runtime gated paths,
  per the M1 fix in W1.

## Required execution order

1. **Wave-level audit** of this package via Codex
   (`audits/wave2-audit-A1.md`). Address any findings.
2. **Open S0** (journal contract extension). Implement, commit,
   run S0 slice-level audit (`audits/S0-A1.md`).
3. **Only after S0 audit APPROVEs**, open the main slices per the
   dependency edges in `slices.md`. Default operator preference is
   strict sequential S0 → S1 → S2 → S3 → S4 → S5 → S6 → S7 → S8.
   Parallelism is allowed only with per-slice audit gates.
4. **S8 (live mainnet validation)** is operator-driven. The wave
   can close with S8 deferred if the in-process Consigliere run
   isn't immediately feasible; closeout records the defer reason
   and a follow-up date.
5. **Wave closeout.** Write `evidence/closeout.md`, mark wave
   `done` in this `master.md` and in the parent program
   `master.md` ledger.
6. **Stop.** Generate the Wave 3 audit prompt; wait for the user's
   Codex response before opening `reorg-handling-wave`.

## Validation

Per-slice validation lives in `slices.md`. Wave-level:

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W2 baseline.
- `SeenBySourcesProjectionTests` green — proves
  `SeenBySources` accumulates `p2p` + `bitails` for the same txid.
- **`PerSessionFrameDispatcher` fan-out + `TxRelayCoordinator` /
  mempool race regression tests** green (audit W2 H1) — no frame
  starvation across 1 K-event mixed-traffic fixture.
- Watchlist scale benchmark: 500 K load ≤ 2 s; lookup p99 ≤ 100 ns
  on the reference CPU class declared in `slices.md` §S7
  reproducibility block.
- `evidence/watchlist-bench.md` exists with host metadata, corpus
  seed, and all required measured fields.
- `evidence/live-validation.md` exists OR closeout explicitly
  defers S8 with a follow-up date.
- Static check, scoped to code only:
  `rg -n "AppendAsync\(.*,.*,.*\"p2p\"" src tests` returns the
  expected number of call sites (matches per S0 + S4 + S5
  implementations).

## Closeout

- `audits/S0-A1.md` — slice-level audit on journal contract
  extension.
- `audits/wave2-audit-A1.md` — pre-execution wave audit.
- `audits/wave2-audit-A2.md` — wave-level audit after all
  slices close (or earlier follow-up audits as `A1-followup.md`
  etc., per the W1 pattern).
- `evidence/watchlist-bench.md` — load + lookup microbenchmark.
- `evidence/live-validation.md` — single live mainnet hit
  (operator-driven; may be deferred).
- `evidence/closeout.md` — end-state metrics, delivery hashes per
  slice, residuals, handoff facts for W3 / W4 / W5 / W6.
- Parent program `master.md` Delivery Notes gets the Wave 2
  closeout commit hash; program ledger row for Wave 2 transitions
  to `done`.

## Commit / report expectations

- **Docs-only commit** when this package is created / revised:
  `docs(p2p): add bsv-mempool-observer-wave package` for creation;
  `docs(p2p): revise wave2 package per audit ...` for revisions.
- **Implementation commits** during execution: one per closed slice
  is preferred. The S0 commit message must call out the journal
  contract extension explicitly so reviewers can spot any later
  silent changes to the journal surface.
- **Wave closeout commit** records commit hashes inside this
  wave's `master.md` Delivery Notes and the parent program ledger.

## Stop conditions

- After S0 closes: pause execution, generate the S0 slice-level
  audit prompt, wait for the user's Codex audit response, only then
  open S1-S8.
- After the wave closes: pause execution, generate the Wave 3 audit
  prompt, wait for the user's Codex response, only then open
  `reorg-handling-wave`.

Default `next-wave-first` mode per the `durable-wave-package`
skill. Do not chain slices or waves silently.
