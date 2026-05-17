---
created: 2026-05-17
type: wave
parent: consigliere-thin-node-observer-program
status: draft (awaiting wave-level Codex audit A1)
---

# Wave 3 — Reorg Handling

## Goal

Detect blockchain reorganisations from the existing W1 headers chain,
turn them into `BlockObservation(Disconnected)` journal events that
the existing `TxLifecycleProjectionRebuilder` already consumes,
notify SignalR clients via the W1-frozen
`IWalletHub.OnReorg(ReorgEventDto)` emitter, and actively
re-broadcast orphaned transactions back into the network mempool
so wallets don't silently lose payments after a fork resolves.

Business outcome: Consigliere stays correct through chain
re-orgs — confirmed transactions that fall out of the active chain
are marked `Reorged` in projections, the SignalR feed surfaces the
event, and the still-valid txs are re-announced to peers so they
can land in the new tip's mempool / next block. No silent
chain-divergence-corrupts-projection class of bug.

## Product Decision

`ReorgEventDto` and `IWalletHub.OnReorg(ReorgEventDto)` are
**already frozen** by Wave 1 S0.8 — W3 implements the emitter
against the frozen surface. `TxLifecycleStatus.Reorged` already
exists as a projection state; the existing rebuilder already moves
projections into it on `BlockObservation(Disconnected)` — W3 just
generates the right journal events from P2P-detected reorgs.
No new public REST/SignalR surface is added in W3.

The only externally visible delta:

1. `IWalletHub.OnReorg(ReorgEventDto)` actually fires (instead of
   the W1 stub never being called).
2. Orphaned-block transactions are re-announced via `inv(MSG_TX)`
   on Ready peers (silent network behaviour — no client-visible
   surface change).

## Scope

In scope:

- **Block journal contract extension (S0 prereq)** — add
  `BlockObservationJournalWriter.AppendDisconnectedAsync(blockHash,
  source, reason?, ct)` overload that mirrors the existing
  `AppendConnectedAsync` semantics: dedupe fingerprint =
  `block.disconnected:{blockHash}:{source}`, idempotent appends
  return `IsDuplicate = true` (parallel to W2 S0-A1 H1 fix). Add
  `BlockObservationSource.Reorg = "reorg"` constant under
  `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservation.cs`.
- **`ReorgDetector`** (`src/Dxs.Bsv/P2p/Chain/ReorgDetector.cs`) —
  pure logic over the W1 `HeadersChain.TryExtend` result.
  `HeadersChain` already produces `Fork(header, parentHeight)`
  when a header builds on a non-tip ancestor; W3 owns the
  cumulative-work / height comparison that promotes a fork to the
  active chain. Produces a `ReorgPlan { CommonAncestorHash,
  CommonAncestorHeight, OrphanedHashes, NewTipHash,
  NewTipHeight, IsDegraded }` record. `IsDegraded = true` when
  the fork point falls below
  `HeadersChainOptions.RetainedHeaderCount` (default 200) — i.e.
  beyond our trailing-window memory.
- **`IOrphanedBlockBodyFetcher`**
  (`src/Dxs.Bsv/P2p/Chain/IOrphanedBlockBodyFetcher.cs` +
  P2P implementation under `src/Dxs.Consigliere/Services/P2p/`)
  — issues `getdata(MSG_BLOCK, blockHash)` to a Ready peer,
  awaits the block frame via the W2-S5
  `PerSessionDispatcherRegistry`, validates the body's merkle
  root against the stored header, returns
  `(BlockHeader header, IReadOnlyList<string> txIds)`. Size + time
  caps: `MaxFetchedBlockBytes` (default 256 MiB) and
  `BlockFetchTimeoutMs` (default 60_000). Mismatched-body
  responses are rejected and the next Ready peer is tried (up to
  3 attempts).
- **`ReorgEventEmitter`**
  (`src/Dxs.Consigliere/Services/P2p/ReorgEventEmitter.cs`) —
  hosted-service that listens to `ReorgDetector` plans. For each
  plan it:
  1. Fetches each orphaned block body via the fetcher
     (skipped if `IsDegraded`).
  2. Calls
     `BlockObservationJournalWriter.AppendDisconnectedAsync(
        blockHash, "reorg", reason: "fork:{forkPointHash}")`
     for each orphaned hash in order. The journal-replay
     pipeline downstream
     (`TxLifecycleProjectionRebuilder.ApplyBlockObservationAsync`)
     transitions affected projections to `Reorged`.
  3. Emits a single
     `IWalletHub.OnReorg(ReorgEventDto)` via
     `IHubContext<WalletHub, IWalletHub>` to the
     `block:tip` group with the plan's data and
     `DegradedState = plan.IsDegraded`.
  4. Forwards the fetched orphaned-block tx lists to
     `OrphanedTxRebroadcaster` for re-broadcast.
- **`OrphanedTxRebroadcaster`**
  (`src/Dxs.Consigliere/Services/P2p/OrphanedTxRebroadcaster.cs`)
  — for each txid extracted from an orphaned block:
  - Skip if the tx is the coinbase of that block (coinbases are
    block-bound; they cannot be re-broadcast).
  - Look up raw bytes via fallback chain: first
    `OutgoingTransactionStore.GetOrNullAsync(txid)`
    (RawHex field), then
    `IRawTransactionPayloadStore.LoadByTxIdAsync(txid)`.
  - If raw bytes found: call
    `TxRelayCoordinator.AnnounceAsync(txId, rawHex, ct)` — reuses
    the W2 announce path that filters
    `BsvP2pHealth.ActiveSessions` for `PeerSessionState.Ready`.
  - If missing: increment `OrphanedTxRebroadcastSkippedNoRaw`
    counter (no raw bytes recorded — cannot re-broadcast). Same
    counter pattern as W2 `SourceObservationRecorder`.
  - Counters: `OrphanedTxAnnounced`, `OrphanedTxSkippedCoinbase`,
    `OrphanedTxSkippedNoRaw`, `OrphanedTxAnnounceFailed`.
- **Idempotent disconnect events.** Journal dedupe fingerprint
  (S0) makes a repeat `AppendDisconnectedAsync(sameBlockHash)`
  return `IsDuplicate = true`; rebuilder is replay-safe by
  construction (already journal-driven). A multi-source reorg
  emitter (e.g. P2P detects + a future operator-forced rescan)
  cannot double-transition projections.
- **Deep-reorg degraded-state path.** When `ReorgDetector` returns
  `IsDegraded = true`, the emitter skips body-fetch + journal
  emission (we don't have those headers anyway) and fires a
  single `OnReorg` with `DegradedState = true`,
  `OrphanedHashes = []`. Operator alarm + admin status surface
  (W6 will wire alerts; W3 records the degraded flag in
  `BsvP2pHealth.LastDegradedReorgAt` so the admin
  `/api/admin/p2p/health` endpoint surfaces it).
- **DI / hosted-service wiring (S5)** under
  `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` — register
  `ReorgDetector`, `P2pOrphanedBlockBodyFetcher`,
  `ReorgEventEmitter` (as `IHostedService`), and
  `OrphanedTxRebroadcaster` (singleton). Mirrors W2's
  `BsvP2pSetup.AddBsvP2pZoneServices` extension.
- **DI regression test (S5 sub-slice).** Extend
  `tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests`
  to resolve every new W3 singleton against mocked external
  dependencies — same pattern as the W2 A2-C1 fix that catches
  ctor drift at build time instead of host startup.
- **End-to-end fixture suite (S6).** 1-deep / 2-deep / 5-deep
  fork tests through `MiniBsvServer`: alternate chain advertised
  via `headers` message → detector promotes → emitter fetches
  bodies via `getdata MSG_BLOCK` (server replies with the
  pre-built orphan body) → assertions on journal, projection
  state, hub event payload, re-broadcast announce. Plus
  mismatched-body (server replies with wrong-merkle-root block),
  idempotent-replay, and degraded-state (fork point >
  `RetainedHeaderCount`) scenarios.
- **Live mainnet validation (S7).** Operator-driven, deferred —
  pass condition: a real mainnet reorg (1-deep is observed
  several times per day on BSV) is detected, OnReorg fires,
  at least one affected projection transitions to `Reorged`,
  evidence recorded under `evidence/live-validation.md`.

Out of scope:

- Per-tx `OnTransactionDeleted` hub events for orphaned tx — the
  parent program's "validation" line mentions this but the
  existing hub surface has no `OnTransactionDeleted` event
  (only `OnTransactionFound`); adding it would be a contract
  amendment to Wave 1 S0. The `Reorged` projection state +
  `OnReorg` event already give clients a complete signal to
  refresh affected tx; the per-tx deletion stream is deferred
  to a future wave (likely W6 ops surface). **The wave-level
  Codex audit should confirm this scope cut.**
- Re-broadcast retry queues / backoff. W3 fires one
  announce-per-Ready-peer pass per reorg; if peers reject the
  tx (e.g. orphaned-input chain), the failure is counted but no
  retry loop runs. The mempool path (W2) will pick up the tx
  again if a peer later relays it. Retry queues are W6
  production-ops scope.
- Block-body re-broadcast (we do not announce orphaned blocks —
  blocks are propagated by miners, not by Consigliere).
- Header sync repair beyond the retention window. The degraded
  flag is informational; full chain rescan is out of scope.
- Source-policy decisions on which peer to ask for orphaned
  block bodies. We use the first Ready peer; failover to the
  next on timeout / mismatch. Per-peer scoring is W6.
- Reorg detection on non-BSV chains. W3 is BSV-specific
  (consistent with W1 / W2 scope). Multi-chain generalisation
  is post-release per the program backlog.
- New `PeerSession` callbacks or hub events. All emission goes
  through Wave 1's frozen `IWalletHub.OnReorg`. If a slice
  surfaces a missing callback, open an explicit contract-freeze
  amendment slice in `docs/stream-tasks/bsv-headers-chain-wave/`
  first — don't add silently here (same rule as W2).

## Core Rules

1. **No new contract surface.** `ReorgEventDto`,
   `IWalletHub.OnReorg`, `IBlockHeaderStore`, `BlockObservation`
   are frozen by W1 S0. W3 implements against them. Internal
   `BlockObservationJournalWriter.AppendDisconnectedAsync` is a
   new method (not a contract — internal infra), parallel to the
   existing `AppendConnectedAsync`.
2. **S0 lands first with its own audit.** `S0` extends
   `BlockObservationJournalWriter` and adds the
   `BlockObservationSource.Reorg` constant. Slice-A1 audit gates
   S1-S7 opening (same pattern Waves 1 + 2 followed).
3. **`IsDuplicate` propagation contract.** Every
   `Append*Async` call MUST return `!result.IsDuplicate` (true =
   newly-appended; false = already-seen). Audit W2 S0-A1 H1 fix
   is the precedent — repeating it here.
4. **Cumulative work vs height for fork comparison.** BSV uses
   cumulative-work for the longest-chain rule. W1's `HeadersChain`
   stores `BlockHeader` records but does NOT track per-header
   work. For W3's first cut we approximate work with height (BSV
   difficulty is roughly stable across a 200-header window). The
   `ReorgDetector` exposes `IFork ComparisonStrategy` so a future
   wave can swap in cumulative-work without re-shaping callers.
   **Wave-level audit should confirm this approximation is
   acceptable for the 200-header window.**
5. **Block-body merkle validation is mandatory.** Every fetched
   orphaned block's coinbase + tx list MUST hash to the
   `MerkleRoot` recorded in the header. Mismatched bodies are
   rejected (counter + log + next-peer retry). No "trust the
   peer" fallback — a malicious peer feeding wrong bodies must
   not corrupt projection state.
6. **Coinbase exclusion on re-broadcast.** The first tx in any
   block is its coinbase; coinbases are block-bound and CANNOT
   be re-broadcast. `OrphanedTxRebroadcaster` MUST skip them
   (counter `OrphanedTxSkippedCoinbase`).
7. **Idempotency by journal dedupe.** Replay-safety comes from
   `AppendDisconnectedAsync` returning `IsDuplicate` on repeat
   calls; the rebuilder's `ApplyBlockObservationAsync` is
   already replay-safe (sets `LifecycleStatus = Reorged`
   idempotently). No additional dedupe layer needed.
8. **Re-broadcast uses W2's announce path.** `TxRelayCoordinator
   .AnnounceAsync` is the single announce primitive — same
   semantics as Gate-3 outbound and the (future) admin-forced
   re-broadcast. Don't add a parallel inv-sending code path.
9. **Hub event ordering: journal first, then emit.** The emitter
   appends every `Disconnected` journal entry BEFORE firing
   `OnReorg`. Clients react to the hub event by re-querying the
   affected projections; those queries must observe the
   `Reorged` state. (The projection rebuilder is journal-driven
   and runs in-process; by the time the hub event fires the
   rebuilder will have caught up to the appended sequence.)
10. **Stop-and-audit per wave** (program rule). S0 gets its own
    slice-level audit before S1-S7 open. S1-S6 are covered by
    the single wave-level audit at `audits/wave3-audit-A1.md`
    after they all close; S7 may close after the audit if it's
    operator-deferred.
11. **W2 prereq dependency.** Every slice in this wave has the
    implicit dependency "Wave 2 closed" — `TxRelayCoordinator
    .AnnounceAsync`, `PerSessionDispatcherRegistry`,
    `BsvP2pHealth`, `IRawTransactionPayloadStore`, and the
    journal-replay pipeline must already be live. The program
    ledger marks W2 done as of 2026-05-17 (commit `844b6e0`).

## Ownership Zones

Program zones touched in this wave:

| Program zone | Repo zone | Files (new unless noted) |
|---|---|---|
| `bsv-p2p-chain` | `bsv-protocol-core` | `src/Dxs.Bsv/P2p/Chain/{ReorgDetector,ReorgPlan,ReorgDetectorOptions,IOrphanedBlockBodyFetcher}.cs`; `src/Dxs.Bsv/P2p/Chain/HeadersChain.cs` (no signature changes — read-only consumer of existing `ExtendResult.Fork`) |
| `bsv-runtime-ingest` | `bsv-runtime-ingest` | `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservationSource.cs` (new — `Reorg` constant + Node / JungleBus stubs for symmetry with `TxObservationSource`). The existing `BlockObservation` record + `BlockObservationEventType.{Connected,Disconnected}` already cover W3's needs; reorg-induced disconnects discriminate via `Source = "reorg"`, not a new event type. |
| `consigliere-block-journal` | `indexer-state-and-storage` | `src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs` (+ `AppendDisconnectedAsync` overload, `IsDuplicate` propagation) |
| `consigliere-p2p-services` | `indexer-ingest-orchestration` | `src/Dxs.Consigliere/Services/P2p/{P2pOrphanedBlockBodyFetcher,ReorgEventEmitter,OrphanedTxRebroadcaster,OrphanedTxRebroadcastRecorder}.cs`; `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (+ `LastDegradedReorgAt` field) |
| `consigliere-config` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (extend — `MaxFetchedBlockBytes`, `BlockFetchTimeoutMs`, `MaxBlockFetchRetries`) |
| `consigliere-setup` | `service-bootstrap-and-ops` | `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (extend — register S2-S5 services) |
| `consigliere-tx-projection` | `indexer-state-and-storage` | `src/Dxs.Consigliere/Data/Transactions/TxLifecycleProjectionRebuilder.cs` (unchanged — already handles `BlockObservation(Disconnected)`; W3 is a journal-event producer, the rebuilder is the consumer) |
| `program-tests` | `verification-and-conformance` | `tests/Dxs.Bsv.Tests/P2p/Chain/ReorgDetectorTests.cs`; `tests/Dxs.Consigliere.Tests/P2p/Reorg/{ReorgEventEmitterIntegrationTests,OrphanedTxRebroadcasterTests,P2pOrphanedBlockBodyFetcherTests}.cs`; extend `tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests.cs` |
| `program-docs` | `repo-governance` | `docs/stream-tasks/reorg-handling-wave/` |

### Handoff facts → consumers

| Consumer | Consumes | Allowed change | Forbidden without amendment |
|---|---|---|---|
| W4 source-metrics | `OrphanedTxRebroadcastRecorder` counters (`OrphanedTxAnnounced` etc.); `BsvP2pHealth.LastDegradedReorgAt` for admin display | implement metrics aggregator on top | rename counter method shape |
| W5 broadcast-unification | `TxRelayCoordinator.AnnounceAsync` is the only announce path — W3 uses it, W5 should not introduce a parallel one | unchanged | parallel announce paths |
| W6 production-ops | `BsvP2pHealth.LastDegradedReorgAt` for alarm wiring; `OrphanedTxRebroadcaster` lifecycle for ops dashboard | observe via existing health surfaces | inject new lifecycle hooks |

## Slice Ledger

Status vocabulary: `done`, `todo`, `in_progress`, `blocked`,
`stale`.

| slice | zone lead | status | depends_on | validation | done_when | audit |
|---|---|---|---|---|---|---|
| S0 | `consigliere-block-journal` (journal contract extension) | todo | — | new method compiles; `dotnet build` green; unit test asserts `IsDuplicate = false` on first append, `true` on repeat with same `(blockHash, source)` fingerprint | `BlockObservationJournalWriter.AppendDisconnectedAsync(blockHash, source, reason?, ct)` lands; `BlockObservationSource.Reorg` constant defined; dedupe fingerprint format documented in `slices.md` §S0 | slice-A1 |
| S1 | `bsv-p2p-chain` (`ReorgDetector` pure logic) | todo | S0 | pure unit tests: 1-deep fork promotes, 2-deep fork promotes, 5-deep fork promotes, fork-point-below-retention returns `IsDegraded`; equal-height tie-break uses first-seen | detector inspects `HeadersChain` state + a new `Fork` candidate and returns a `ReorgPlan` (or `null` if no promotion); pure — no I/O | wave-A1 |
| S2 | `consigliere-p2p-services` (`P2pOrphanedBlockBodyFetcher`) | todo | S0, S1 | integration test via `MiniBsvServer`: server advertises an alternate-chain header, replies to `getdata MSG_BLOCK` with a constructed body; fetcher returns parsed tx list; mismatched-merkle-root response triggers retry on next peer; oversize-block exceeds cap → rejected with counter; timeout → next peer | fetcher returns `(BlockHeader header, IReadOnlyList<string> txIds)` for a valid orphan body; rejects mismatched body; respects `MaxFetchedBlockBytes` + `BlockFetchTimeoutMs`; cycles through Ready peers up to `MaxBlockFetchRetries` | wave-A1 |
| S3 | `consigliere-p2p-services` (`ReorgEventEmitter` hosted service) | todo | S0, S1, S2 | E2E fixture: `HeadersChain` extended via fixture; detector returns plan; emitter calls journal-append per orphan + `IWalletHub.OnReorg(dto)`; verify DTO field values match plan; verify journal calls are in orphan-order; degraded path skips body fetch + journal append, fires single `OnReorg` with `DegradedState = true` | for non-degraded reorg: every orphan block's body is fetched, journal-appended, and a single `OnReorg` fires with the correct DTO; for degraded reorg: single `OnReorg(DegradedState = true)` fires; `BsvP2pHealth.LastDegradedReorgAt` updated | wave-A1 |
| S4 | `consigliere-p2p-services` (`OrphanedTxRebroadcaster` + counters) | todo | S0, S2 | unit tests with fake `OutgoingTransactionStore` + `IRawTransactionPayloadStore` + `TxRelayCoordinator`: tx in outgoing store → announce called; tx in payload store → announce called; coinbase → counter increments + announce NOT called; missing raw → counter increments + announce NOT called; announce failure → counter increments | per orphaned-block tx list, each non-coinbase tx is announced if raw bytes exist; counters increment exactly once per skip / failure / success; no double-announce on repeat reorg replay | wave-A1 |
| S5 | `consigliere-setup` (DI + hosted-service wiring) | todo | S0-S4 | extension test resolves every new W3 singleton against a mocked DI graph (same shape as `BsvP2pSetupDiResolutionTests`); `ReorgEventEmitter` is in `services.GetServices<IHostedService>()` | `BsvP2pSetup.AddBsvP2pZoneServices` registers `ReorgDetector`, `P2pOrphanedBlockBodyFetcher`, `OrphanedTxRebroadcaster`, `OrphanedTxRebroadcastRecorder`, `ReorgEventEmitter` (hosted); all resolve from production DI graph | wave-A1 |
| S6 | `program-tests` (E2E fixture suite) | todo | S0-S5 | 1-deep / 2-deep / 5-deep reorg fixtures driven by `MiniBsvServer`: assert (a) journal contains correct `Disconnected` entries; (b) projection state moves to `Reorged` for tx that lived in orphaned blocks; (c) hub `OnReorg` invoked exactly once per reorg with correct DTO payload; (d) re-broadcast `inv(MSG_TX)` observed at the mini server for each non-coinbase orphan tx with raw available; (e) deep-reorg-beyond-window asserts degraded state, no replay attempted; (f) mismatched-body fetched twice → second peer attempt rejected → final state shows no journal append + body-fetch-failure counter; (g) repeat reorg emission (idempotency) appends `IsDuplicate = true` and produces no double `Reorged` transition | every scenario in `slices.md` §S6 green; benchmark report in `evidence/reorg-bench.md` capturing emitter throughput (≥ 100 reorgs/min in fixture-driven harness) | wave-A1 |
| S7 | live-mainnet validation (operator-driven) | todo | S0-S6 | operator runs Consigliere connected to mainnet; on a natural 1-deep reorg (multiple/day on BSV) `OnReorg` fires within 10 s of the new tip headers arriving; affected projection transitions to `Reorged` and (if raw bytes exist) is re-announced via `inv` | one observed live-mainnet reorg recorded in `evidence/live-validation.md` with timestamps + hub event payload + projection-state snapshot | wave-A1 |

Slice S0 (block journal contract extension) is the **prerequisite
slice** required by the program launch prompt. Its slice-level
audit (`audits/S0-A1.md`) lands before S1-S7 open. S1-S7 are
covered by the single wave-level audit at
`audits/wave3-audit-A1.md` after they all close (S7 may close
after the audit if operator-deferred — same pattern as W2 S8).

## Definition of Done

- All slices `done` (S7 may be operator-deferred with rationale;
  see `slices.md` §S7 if it lands after the rest).
- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test` returns no new failures vs the pre-W3 baseline.
- Reorg fixture suite green: 1-deep, 2-deep, 5-deep, beyond-window,
  mismatched-body, idempotent-replay all PASS.
- E2E: `OnReorg` SignalR event observed by a fake hub client with
  correct DTO; affected `TxLifecycleProjectionDocument` rows show
  `LifecycleStatus = Reorged`; `inv(MSG_TX)` for each
  re-broadcast tx observed at the mini server.
- DI regression test green:
  `BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves`.
- Live mainnet validation recorded in
  `evidence/live-validation.md` (or explicitly deferred to a
  post-wave operator session with the reason in
  `evidence/closeout.md`).
- Wave-level Codex audit at `audits/wave3-audit-A1.md` returns
  APPROVE (or APPROVE WITH CHANGES addressed in-wave).
- `evidence/closeout.md` lists delivery hashes per slice and
  end-state metrics.

## Delivery Notes

Commit hashes recorded here as slices close.

- Wave package created: this commit (initial draft)
- Wave audit A1: pending
