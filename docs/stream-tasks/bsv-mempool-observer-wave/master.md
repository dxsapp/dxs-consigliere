---
created: 2026-05-17
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting wave-level Codex audit)
---

# Wave 2 — BSV Mempool Observer

## Goal

Make the BSV P2P pool a first-class transaction-observation source,
equal to the existing Bitails / JungleBus realtime runners. Every
`inv(MSG_TX)` from a connected peer triggers a rate-limited
`getdata` fetch, deduplication, watchlist matching, and append to the
existing `TxObservationJournalWriter` with `SeenBySources += "p2p"`.
Watchlist matching uses a `HashSet<ulong>` over 8-byte prefixes of
`Address.Hash160`, full-hash160 verify on hit, hot-reloaded from
RavenDB. Bitails and JungleBus realtime runners stay alive — Wave 4
will measure how each source contributes — but their writes get
explicit source tags so the journal stays source-aware.

Business outcome: Consigliere can keep tracking transactions for
managed addresses and tokens **without depending on a self-hosted
bitcoin-sv node**, with P2P observations carrying the same
write-path as Bitails / JungleBus and feeding the same
`TxLifecycleProjectionDocument`.

## Product Decision

Mempool observation is internal Consigliere infrastructure. No new
public REST/SignalR surface is added in W2; the existing
`OnTransactionFound` callback continues to fire on watchlist hits.
The only externally visible delta is that `SeenBySources` now
includes `p2p` when a P2P observation came in (per Wave 1 contract
freeze, the DTO did not change).

## Scope

In scope:

- Extension of `TxObservationSource` with `P2p` constant
  (`src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs`).
- Source-neutral overload on
  `TxObservationJournalWriter.AppendAsync(...)` that takes a
  `TxObservation` + optional `RawTransactionPayloadReference` + source
  directly (the existing `TxMessage`-shaped overload stays for the
  Bitails / JungleBus path; nothing else moves to the new overload
  in W2 — incremental migration).
- Projection rebuild test proving `SeenBySources` accumulates `p2p`
  alongside `bitails` / `junglebus` when journal contains observations
  from multiple sources for the same txid.
- `WatchlistMatcher` — pure in-memory matcher backed by a
  `HashSet<ulong>` over the 8-byte prefix of `Address.Hash160`, with
  full-hash160 verification on hit. Token matching parses every tx
  outputs against `WatchingToken.TokenId` when any token is watched
  (same limitation as today's Bitails realtime scope).
- `RavenWatchlistLoader` — initial bulk load from `WatchingAddress` /
  `WatchingToken` and hot-reload via Raven Subscription API; pushes
  add / remove deltas into the matcher.
- `TxScriptParser` — extracts P2PKH `Hash160` from outputs + inputs,
  and parses token outputs sufficient to read `TokenId`. Reuses
  primitives from `src/Dxs.Bsv/Script/` where they exist.
- `MempoolWatcher` — dedupe `inv(MSG_TX)` items across peers, gate
  `getdata` requests under a rate limit, route received `tx` payloads
  through `WatchlistMatcher` and (on hit) through
  `TxObservationJournalWriter`. Explicit `MaxFetchedTxBytes` policy
  (default 32 MiB, raised from the legacy 2 MiB session default)
  with oversize-payload accounting — see `slices.md` §S4 payload
  section.
- `PerSessionFrameDispatcher` + `PerSessionDispatcherRegistry`
  (new Consigliere infrastructure) — owns the single consumer of
  `PeerSession.IncomingMessages` per session and fans frames out
  to multiple subscribers. `TxRelayCoordinator` is refactored in
  the same slice to consume via the dispatcher (behavioural
  parity). Audit W2 H1 fix.
- `P2pMempoolIngestRunner` — `IHostedService` that wires
  `PeerSession.OnInvReceived` (frozen S0 surface from W1) into
  `MempoolWatcher`, subscribes one-shot `tx`-frame handlers via the
  dispatcher to await getdata replies, and routes payloads through
  the watcher. Parallel to `BitailsRealtimeIngestRunner` /
  `JungleBusRealtimeIngestRunner`.
- **S6 is a regression-pin slice only** — current Bitails and
  JungleBus runners already tag their observations correctly via
  `TxMessage.Source`. W2 adds source-tag assertions to the
  existing runner tests but makes no production-code edits in
  those runners. See `slices.md` §S6 + audit W2 M2 reconciliation.
- Watchlist correctness fixture suite: address-output, address-input,
  STAS / DSTAS token output, removal of address while observer
  running, `HashSet<ulong>` collision behaviour.
- Watchlist 500 K-address load benchmark + microbenchmark for hot-path
  matcher lookup (p99 ≤ 100 ns target, per program validation
  matrix).
- Real-mainnet validation: tx paying a watched address surfaces via
  `WalletHub.OnTransactionFound` end-to-end inside Consigliere.

Out of scope:

- Per-source metrics dashboard — W4.
- Reorg state transitions (`BlockDisconnected`, re-broadcast) — W3.
- Watchlist UI management — already exists in
  `AdminTrackedController`; W2 just reads the current entries.
- Removal of HTTP-provider broadcast paths — W5.
- Peer scoring / rotation / alerts — W6.
- New hub events or `PeerSession` callbacks — those are frozen by
  Wave 1 S0; W2 implements against the frozen surface only.

## Core Rules

1. **No new contract surface.** Wave 1 S0 froze `PeerSession`
   callbacks, hub events, and `BroadcastReceiptDto`. W2 implements
   against the frozen surface only. If a slice surfaces a need that
   S0 missed, open an explicit contract-freeze amendment slice in
   `docs/stream-tasks/bsv-headers-chain-wave/` first — don't add
   silently here.
2. **Source-neutral journal append lands first.** S0 (this wave's
   prerequisite slice) extends
   `TxObservationJournalWriter.AppendAsync` to accept a `TxObservation`
   + payload + source directly, and adds `TxObservationSource.P2p`.
   No P2P observation code lands before S0 closes — same gate
   pattern Wave 1 used for its S0.
3. **`HashSet<ulong>` watchlist, not bloom filter.** Matcher keys are
   the first 8 bytes of `Address.Hash160` interpreted little-endian
   as `ulong`. Full hash160 verify on positive match guards against
   the (rare) 8-byte prefix collision. No bloom filter in this
   program — sized for ≤ 500 K addresses with 1 M as the soft
   upper-bound benchmark.
4. **Watchlist is hot-reloaded from Raven.** `RavenWatchlistLoader`
   uses Raven Subscription API to push add / remove deltas into the
   in-memory matcher without restart. Removal of a watched address
   while the observer is running must produce no false matches on
   the next observed tx.
5. **Token matching widens scope.** If any `WatchingToken` is
   present, the matcher parses every observed tx output for token
   markers (same as today's Bitails realtime scope provider). This
   is acceptable because token watchlists are typically small and
   the parsing cost is dominated by IO.
6. **Bitails / JungleBus runners stay alive.** Their behaviour does
   not change in W2 beyond explicit source-tagging through the new
   journal overload. Per-source metrics (W4) will measure each
   source's contribution; W2 only ensures the data is source-tagged
   consistently.
7. **Wave 1 prereq dependency.** Every slice in this wave has the
   implicit dependency "Wave 1 closed" — `PeerSession.OnInvReceived`,
   `BlockHeaderStore`, `HeadersChain`, and the `IPeerTelemetrySink`
   wiring must already exist. The program ledger marks W1 done as
   of 2026-05-17.
8. **Stop-and-audit per wave** (program rule). S0 gets its own
   slice-level audit before S1-S8 open, mirroring the W1 pattern.
9. **`PeerSession.IncomingMessages` is single-consumer.** Audit
   W2 H1 reconciliation. `ChannelReader<T>` is not a broadcast
   primitive — each frame is delivered to exactly one consumer.
   `TxRelayCoordinator` (Gate 3) currently reads the channel
   per session; W2's mempool runner must NOT add a parallel
   reader on the same session. S5 introduces a
   `PerSessionFrameDispatcher` in Consigliere that owns the
   single reader and fans frames out to multiple subscribers
   (relay coordinator + mempool watcher + future consumers).
   `TxRelayCoordinator` is refactored in S5 to consume via the
   dispatcher — behavioural parity. Adding a new `OnTxReceived`
   `PeerSession` callback is **forbidden** (would violate Wave 1
   S0 contract freeze).

## Ownership Zones

Program zones touched in this wave:

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `bsv-p2p-session` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Session/PeerSession.cs` (no signature changes — consumes frozen S0 callbacks only) |
| `bsv-p2p-observer` (new) | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Observer/{MempoolWatcher,MempoolWatcherOptions,WatchlistMatcher,MatchResult,TxScriptParser}.cs` |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/{P2pMempoolIngestRunner,RavenWatchlistLoader,SourceObservationRecorder,PerSessionFrameDispatcher,PerSessionDispatcherRegistry}.cs`; `src/Dxs.Consigliere/Services/P2p/TxRelayCoordinator.cs` (refactor to use the dispatcher; behavioural parity) |
| `consigliere-p2p-tasks` | `indexer-ingest-orchestration` | (none new in W2 — runner is hosted-service in `consigliere-p2p-services`) |
| `consigliere-tx-projection` | `indexer-state-and-storage` | `src/Dxs.Consigliere/BackgroundTasks/TxObservationJournalWriter.cs` (+ source-neutral overload); `src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjectionRebuilder.cs` (unchanged — read-only consumer of new `Source` value) |
| `bsv-runtime-ingest` | `bsv-runtime-ingest` (per `docs/repository-zones/zone-catalog.md` line 13: `src/Dxs.Bsv/{BitcoinMonitor,Rpc,Zmq,Factories}/**`) | `src/Dxs.Bsv/BitcoinMonitor/Models/TxObservation.cs` (+ `P2p` source constant) |
| `consigliere-p2p-realtime` | `indexer-ingest-orchestration` | (W2 leaves both runners' production code untouched — S6 is regression-test pin only; see `slices.md` §S6) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/Observer/`, `tests/Dxs.Consigliere.Tests/P2p/`, `tests/Dxs.Consigliere.Tests/BackgroundTasks/Realtime/*` (S6 pin), watchlist scale microbenchmark under `tests/Dxs.Consigliere.Benchmarks/` |
| `program-docs` | `repo-governance` | `docs/stream-tasks/bsv-mempool-observer-wave/` |

**Audit W2 M3 reconciliation:** `src/Dxs.Bsv/BitcoinMonitor/`
belongs to `bsv-runtime-ingest` per the zone catalog precedence
rule, not `indexer-state-and-storage` (which is Raven-document
state inside Consigliere). The earlier draft mislabelled it; the
table above corrects this. `SourceObservationRecorder` is
Consigliere orchestration state — labels like `bitails` /
`junglebus` / `p2p` are Consigliere-level, not BSV-protocol — and
lives under `src/Dxs.Consigliere/Services/P2p/`. The parent
program's `slices.md` initially listed it under
`src/Dxs.Bsv/P2p/Observer/`; a parallel parent-program update in
this revision moves it to align.

Out-of-catalog ad-hoc:

| Ad-hoc zone | Purpose | Files |
|---|---|---|
| `mempool-soak-spike` (optional) | If end-to-end mainnet validation needs a separate harness similar to `HeadersSoakRecorder` | `tests/Spikes/P2p/MempoolWatcherSoak/` (only if S8 evidence requires it; default is in-process Consigliere run) |

### Handoff facts → consumers

| Consumer | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| W3 reorg-handling-wave | `TxObservation` with `Source = "p2p"` in journal; `SeenBySources` accumulation semantics | extend `RebroadcastSkippedReason` field on projection or sidecar doc | rename `TxObservationSource.P2p`; remove the source-neutral journal overload |
| W3 | `WatchlistMatcher` for re-broadcast decisions on watched tx | read-only consume | mutate matcher API |
| W4 source-metrics | `SourceObservationRecorder` raw-counter signal (before dedupe) | implement metrics aggregator on top | change recorder method shape |
| W4 | Existing observation source tags (`p2p`, `bitails`, `junglebus`) | rely on them as deterministic strings | rename any of these constants |
| W5 broadcast-unification | `TxObservation` shape unchanged | unchanged | n/a |
| W6 production-ops | `P2pMempoolIngestRunner` lifecycle for ops dashboard | observe via existing health surfaces | inject new lifecycle hooks |

## Slice Ledger

Status vocabulary: `not_opened`, `todo`, `in_progress`, `blocked`,
`done`, `stale`.

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `consigliere-tx-projection` (journal contract extension) | not_opened | — | new files compile; `dotnet build` green; `dotnet test` baseline green; projection rebuild test asserts `SeenBySources` includes `p2p` after replay of a P2P + Bitails observation pair for the same txid | `TxObservationSource.P2p` added; source-neutral `AppendAsync(TxObservation, RawTransactionPayloadReference?, string source)` overload added; projection rebuild test green | slice-A1 |
| S1 | `bsv-p2p-observer` (TxScriptParser) | not_opened | S0 | pure unit tests on P2PKH script → `Hash160` extraction; STAS / DSTAS token-output `TokenId` extraction; reject malformed scripts | parses canonical P2PKH outputs and inputs, STAS / DSTAS token outputs; rejects malformed scripts without exception | wave-A1 |
| S2 | `bsv-p2p-observer` (WatchlistMatcher pure logic) | not_opened | S0, S1 | unit tests: add / remove address; positive match on prefix + full-hash verify; negative match on prefix-collision-but-different-full-hash; token-output match | matcher resolves address + token in O(1) hot path; full-hash verify catches 8-byte prefix collision | wave-A1 |
| S3 | `consigliere-p2p-services` (RavenWatchlistLoader) | not_opened | S0, S2 | Raven integration test: initial bulk load from `WatchingAddress` / `WatchingToken`; subscription delta add / remove pushes into matcher; benchmark: 500 K addresses load wall-clock ≤ 2 s | loader populates matcher on startup; subscription deltas reflect in matcher within 200 ms; 500 K-address load benchmark ≤ 2 s | wave-A1 |
| S4 | `bsv-p2p-observer` (MempoolWatcher core) | not_opened | S0, S2 | unit tests: dedupe across peers (one getdata per txid); rate-limit enforcement; route matched tx to journal; route unmatched tx through `SourceObservationRecorder` only | inv(MSG_TX) → at most one getdata per txid per N seconds; matched tx persisted via journal; unmatched tx counted but not persisted | wave-A1 |
| S5 | `consigliere-p2p-services` (PerSessionFrameDispatcher + P2pMempoolIngestRunner) | not_opened | S0, S2, S3, S4 | dispatcher fan-out test; `TxRelayCoordinator` + mempool runner race regression on the same session (audit W2 H1); hosted-service test: fake peer pushes inv → service sends getdata → fake peer responds with tx → journal append observed; existing Gate-3 broadcast tests still green after `TxRelayCoordinator` refactor | dispatcher routes frames to multiple subscribers; `TxRelayCoordinator` migrated to consume via the dispatcher (behavioural parity); runner attaches `OnInvReceived` per Ready peer, drives watcher via one-shot tx-frame handlers, no race or starvation in 1k-event fixture | wave-A1 |
| S6 | `consigliere-p2p-realtime` (Bitails / JBus source-tag **regression pin**) | not_opened | S0 | regression-test assertion: every captured `TxMessage.Source` from each runner is the expected constant; no production-code edits in W2 (current runners already tag correctly per audit W2 M2) | both runner test suites still green with the source-tag assertion added; PR diff for S6 touches only the two runner test files | wave-A1 |
| S7 | `program-tests` (watchlist correctness fixture suite) | not_opened | S0, S1, S2, S3 | fixture suite covers address-output (P2PKH), address-input (spending tx), STAS / DSTAS token output, removal-during-observation, prefix collision; microbenchmark p99 ≤ 100 ns | every fixture scenario in §S7 of `slices.md` green; microbenchmark report in `evidence/watchlist-bench.md` | wave-A1 |
| S8 | live-mainnet validation (operator-driven) | not_opened | S0–S7 | operator runs Consigliere mainnet with a known watched address; tx paying that address triggers `WalletHub.OnTransactionFound` within 2 s of inv arrival; `SeenBySources` includes `p2p` in the projection | one observed live-mainnet hit recorded in `evidence/live-validation.md` with timestamps + projection snapshot | wave-A1 |

Slice S0 (journal contract extension) is the **prerequisite slice**
required by the program launch prompt. Its slice-level audit
(`audits/S0-A1.md`) lands before S1-S8 open. S1-S8 are covered by
the single wave-level audit at `audits/wave2-audit-A1.md` after they
all close.

## Definition of Done

- All slices `done` (S8 may be operator-deferred with rationale; see
  `slices.md` §S8 if it lands after the rest).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W2 baseline.
- Projection rebuild test green: `SeenBySources` accumulates
  `p2p` alongside `bitails` / `junglebus` for the same txid.
- Watchlist scale benchmark: 500 K addresses load ≤ 2 s wall-clock;
  matcher hot-path lookup p99 ≤ 100 ns.
- Live mainnet validation recorded in `evidence/live-validation.md`
  (or explicitly deferred to a post-wave operator session with the
  reason in `evidence/closeout.md`).
- Wave-level Codex audit at `audits/wave2-audit-A1.md` returns
  APPROVE (or APPROVE WITH CHANGES addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and
  end-state metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: `<hash-pending>` (this commit)
- Wave audit A1: pending
- Slice S0 audit A1: pending
- Slice S0 delivery: pending
- Slice S1 delivery: pending
- Slice S2 delivery: pending
- Slice S3 delivery: pending
- Slice S4 delivery: pending
- Slice S5 delivery: pending
- Slice S6 delivery: pending
- Slice S7 delivery: pending
- Slice S8 delivery: pending
- Wave audit A2 (post-execution): pending
- Wave closeout commit: pending
