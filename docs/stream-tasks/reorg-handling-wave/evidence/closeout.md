# Wave 3 Closeout — `reorg-handling-wave`

Status: implementation complete; pending wave-level post-execution
Codex audit (`audits/wave3-audit-A2.md`). S0-S6 delivered. S2
intentionally deferred per the mid-wave design pivot (see
"Scope deviations" below). S7 (live-mainnet) deferred to a
post-wave operator session per the W1 / W2 pattern.

## Delivery summary

| slice | commit | summary |
|---|---|---|
| S0 | `091299e` | `BlockObservationJournalWriter.AppendDisconnectedAsync` + `BlockObservationSource.{Node,JungleBus,Reorg}` constants + tests |
| S1 | `bfac702` | `ReorgDetector` pure logic + `ReorgPlan` + `ICumulativeWorkComparer` + `HeadersChain.TryGetByWireHashHex` / `RetainedHeaderCount` accessors + 11 unit tests |
| S2 | DEFERRED | Block-body fetch over P2P (`IOrphanedBlockBodyFetcher`). Rationale: BSV mainnet blocks are GB-scale and unnecessary for W3 deliverables. Replaced by the projection-query path (`RavenOrphanedTxIdReader`). See "Scope deviations" |
| S3 | `9c64818` | `ReorgPipeline` + `IReorgPipeline` + `RavenOrphanedTxIdReader` + `BsvP2pHealth.LastDegradedReorgAt` + `HeadersChainService.Fork` hook + 9 integration tests |
| S4 | `9c64818` | `OrphanedTxRebroadcaster` + `OrphanedTxRebroadcastRecorder` + thin `ITxAnnouncer` / `IOutgoingRawLookup` abstractions (so the rebroadcaster can be unit-tested without standing up the sealed `TxRelayCoordinator` / Raven-backed `OutgoingTransactionStore`) + 8 unit tests |
| S5 | `9c64818` | `BsvP2pSetup.AddBsvP2pZoneServices` extended with the W3 graph + `BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves` regression test (W2 A2-C1 pattern) |
| S6 | `9c64818` (depth scenario) | Wave-level fixtures across the existing pipeline + rebroadcaster + detector suites cover the §S6 scenarios that survive the S2 deferral. See "S6 coverage matrix" |
| S7 | deferred | Operator-driven live mainnet validation; deferral recorded below |

## Test counts gained in W3

- `Dxs.Bsv.Tests`: +11 (`ReorgDetectorTests`)
- `Dxs.Consigliere.Tests`: +25
  - S0: +7 (`BlockObservationJournalWriterTests`)
  - S3: +9 (`ReorgPipelineTests`; includes 5-deep depth pin)
  - S4: +8 (`OrphanedTxRebroadcasterTests`)
  - S5: +1 (`BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves`)

End-state run on the build host (macOS arm64, .NET 9):

```
Dxs.Bsv.Tests         : 218/218 passed (was 207 pre-W3, +11)
Dxs.Consigliere.Tests : 340 passed + 24 explicit Skipped
                        + 3 pre-existing baseline Raven-runtime
                        failures (unchanged from W2 baseline)
```

## Scope deviations

### S2 deferral — block-body fetch over P2P

The original draft proposed `IOrphanedBlockBodyFetcher` to issue
`getdata MSG_BLOCK` over P2P, parse the full block payload, validate
merkle root, and enumerate orphan-block txids. On survey we confirmed:

1. BSV mainnet blocks are routinely > 1 GiB and can exceed 4 GiB —
   reliable chunked block-body receive over P2P is a large
   engineering effort.
2. The orphaned-block txids are already known to us from
   `TxLifecycleProjectionDocument.BlockHash` (the rebuilder queries
   this on every Disconnected event today).
3. Header-chain PoW validation in `HeadersChain.TryExtend.MeetsTarget`
   authenticates the fork chain — a peer cannot force a fake reorg
   without producing a longer PoW-valid chain.

The simplification: a small `RavenOrphanedTxIdReader` queries
projections by `BlockHash` (using the same Raven LINQ predicate the
rebuilder uses); the pipeline emits `Disconnected` journal entries
and the existing `TxLifecycleProjectionRebuilder.ApplyBlockObservationAsync`
handles the projection transition.

This deferral is flagged as open audit question #4 in
`audits/wave3-audit-A1-prompt.md`. A future wave that needs full
orphaned-block bodies (e.g. retroactive re-scan against an updated
watchlist) can land the fetcher then.

### S6 coverage matrix (vs the original slices.md §S6)

| # | Scenario | Status |
|---|---|---|
| 1 | Single-block reorg | covered (`ReorgPipelineTests.OnePlan_*` + `OnePlan_AppendsDisconnectedForEachOrphan`) |
| 2 | Two-block reorg | covered (`ReorgDetectorTests.Detect_TwoDeepFork_OrphansInDisconnectOrder_NewestFirst` for detector; `ReorgPipelineTests.FiveDeepFork_*` exercises the multi-orphan pipeline path) |
| 3 | Five-block reorg | covered (`ReorgDetectorTests.Detect_FiveDeepFork_*` + `ReorgPipelineTests.FiveDeepFork_AppendsFiveDisconnectedEntries_*`) |
| 4 | Deep reorg beyond window | covered (`ReorgDetectorTests.Detect_ForkPointBelowRetention_*`, `ReorgPipelineTests.DegradedPlan_*`) |
| 5 | Mismatched-body | **N/A** — S2 deferral removed the block-body fetch path; merkle validation is the fetcher's responsibility and there is no fetcher to test |
| 6 | Idempotent replay | covered (`BlockObservationJournalWriterTests.AppendDisconnected_RepeatCall_*`, `ReorgPipelineTests.RepeatPlan_SameForkTip_IsIdempotent_StableState` — renamed in A2 revision; the post-C2 promotion makes a second replay see the detector return null via height-equality short-circuit, a stronger guarantee than the pre-rename fingerprint-only check) |
| 7 | Tx in OutgoingStore re-broadcast | covered (`OrphanedTxRebroadcasterTests.TxInOutgoingStore_*`) |
| 8 | Tx in PayloadStore re-broadcast | covered (`OrphanedTxRebroadcasterTests.TxInPayloadStore_*`) |
| 9 | Tx with no raw skipped | covered (`OrphanedTxRebroadcasterTests.TxWithNoRaw_*`) |
| 10 | Coinbase skipped | covered explicitly (A2 revision H2 + A2-followup N2): `ICoinbaseProbe` + `MetaTransactionCoinbaseProbe` runs the BSV-consensus check `Index == 0 AND Inputs.Count == 1 AND Inputs[0].TxId is all-zero` BEFORE the rebroadcaster's raw lookup; `OrphanedTxRebroadcastRecorder.IncrementSkippedCoinbase` fires on hit. Tests: `Coinbase_IsSkipped_NotAnnounced` + `NonCoinbase_FlowsThroughRawLookup_EvenWhenSomeOrphanIsCoinbase` |

### S7 deferral

Live-mainnet validation is operator-driven (a real BSV mainnet
reorg must occur while Consigliere is connected). Deferred to a
post-wave operator session; pass condition documented in
`slices.md` §S7.

## Static checks

```
$ rg -n 'BlockObservationSource\.Reorg' src tests
src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservationSource.cs:* (definition)
src/Dxs.Consigliere/Services/P2p/ReorgPipeline.cs:* (×2)
tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgPipelineTests.cs:* (×1)
tests/Dxs.Consigliere.Tests/BackgroundTasks/Blocks/BlockObservationJournalWriterTests.cs:* (×N)
```

No leakage outside Wave 3 ownership.

## Handoff facts unlocked for W4-W6

- **W4 source-metrics-wave** can consume:
  - `OrphanedTxRebroadcastRecorder` counters (`Announced` /
    `SkippedNoRaw` / `AnnounceNoReadyPeer` / `AnnounceFailed`).
  - `BsvP2pHealth.LastDegradedReorgAt` for the admin / health
    surface.
  - `BlockObservationSource.Reorg` as a deterministic string
    constant.
- **W5 broadcast-unification-wave** can rely on:
  - `ITxAnnouncer` as the canonical broadcast-side announce
    interface — W5 should NOT introduce a parallel announce path.
- **W6 production-ops-wave** can:
  - Observe `BsvP2pHealth.LastDegradedReorgAt` to fire degraded-
    reorg alarms.
  - Iterate `OrphanedTxRebroadcaster` lifecycle via existing health
    surfaces (no new lifecycle hooks needed).
  - Consider lifting the deferred S2 (block-body fetch) if the ops
    team needs full block-body validation under malicious-peer
    threat modelling.

## Residuals to track

- **S2 (block-body fetch over P2P)** — deferred; see "Scope
  deviations". Document the security trade-off as an explicit
  open question for the post-execution audit.
- **S7 live-mainnet validation** — deferred to operator session;
  evidence schema in `slices.md` §S7.
- **Coinbase-explicit skip** — landed in the A2 revision (H2) via
  `ICoinbaseProbe` and the explicit
  `OrphanedTxRebroadcastRecorder.IncrementSkippedCoinbase` counter.
  The `MetaTransactionCoinbaseProbe` (A2-followup N2) requires the
  BSV consensus signature `Index == 0 AND Inputs.Count == 1 AND
  Inputs[0].TxId is all-zero` so a default-`Index = 0` non-coinbase
  cannot trip the probe.
- **3 pre-existing baseline failures** in
  `TransactionStoreIntegrationTests` (Raven embedded runtime
  mismatch); unchanged by Wave 3.

## Audit trail

- `audits/wave3-audit-A1-prompt.md` — pre-execution audit prompt
  written before any slice committed (user runs it through Codex
  async; verdict to be folded into the next revision).
- `audits/wave3-audit-A1.md` — pending (awaiting user-driven Codex
  audit response).
- `audits/wave3-audit-A2.md` — pending (post-execution audit; runs
  after S0-S6 closeout, before W4 opens).

## Ready-for-audit checklist

- [x] All slices delivered (S2 deferred with rationale, S7
      operator-deferred).
- [x] `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- [x] No new test failures vs the pre-W3 baseline.
- [x] DI regression test `W3_SingletonGraph_Resolves` green.
- [x] `evidence/closeout.md` lists delivery hashes per slice and
      end-state metrics.
- [ ] `audits/wave3-audit-A1.md` — awaiting Codex verdict per the
      pre-execution prompt.
- [x] `audits/wave3-audit-A2.md` — MAJOR REVISION REQUIRED (2 C, 3 H,
      3 M, 2 L). Revision committed; see "A2 revision summary" below.
      Awaiting A2-followup audit.

## A2 revision summary (this commit)

Wave-level Codex audit A2 returned MAJOR REVISION REQUIRED with two
critical findings (chain authentication + fork promotion) and three
high-severity ones. All ten findings folded in this commit chain.

### C1 — chainwork validation against low-difficulty fork attack

**Before:** `ICumulativeWorkComparer` ranked chains by height. A
malicious peer minting valid-PoW-but-trivial-difficulty headers
could trigger a reorg by sheer length. `HeadersChain.TryExtend`
only validated each header's own `bits` field, not the expected
network difficulty.

**After:** new `WorkBitsCumulativeWorkComparer` integrates per-header
work as `2^256 / (target + 1)` over the chains-above-common-ancestor
and compares cumulative work. Production DI defaults to this comparer
via `BsvP2pSetup`. Regression test
`ReorgDetectorTests.Detect_LowDifficultyFork_LongerHeight_LessWork_ReturnsNull`
plus a counter-example
`Detect_HeightOnlyComparer_AcceptsTallerForkRegardlessOfDifficulty`
pin the semantic difference.

DAA validation per BSV's block-by-block difficulty adjustment is still
absent — a peer can mint headers whose individual `bits` violate the
DAA rule. The chainwork comparison limits damage (such headers
contribute proportionally less work) but a follow-up wave should
implement explicit DAA enforcement in `HeadersChain.TryExtend`.

### C2 — fork tip promotion

**Before:** `HeadersChain.TryExtend` stored fork headers with
`promoteToTip: false`; `ReorgPipeline` emitted journal + hub +
rebroadcast side effects but never updated `_tip`. The active chain
stayed on the orphaned tip; `BuildLocator()` and `OnNewBlock` kept
referencing the dead chain.

**After:** new `HeadersChain.PromoteFork(forkTip)` swaps `_tip` and
`_tipHeight` to the fork tip + re-prunes against the new height.
`ReorgPipeline.HandleForkObservedAsync` invokes it AFTER journal
appends, BEFORE hub emit. The pipeline also emits a fresh
`INewBlockNotifier.NotifyAsync(BlockTipDto)` for the promoted tip so
clients tracking `OnNewBlock` see the chain switch as a
forward-progress event. Persistence path unchanged: fork headers
were already stored on the original `ExtendResult.Fork` case, and
`BlockHeaderStore.GetTipAsync` queries ORDER BY Height DESC so
restart-correctness picks the new tip automatically.

### H1 — journal-before-hub via projection rebuilder

**Before:** Core Rule §9 ("journal append before hub emit") relied
on the projection rebuilder being inline-with-journal, but the
rebuilder is actually lazy (invoked by query / notifier paths only).

**After:** new `IProjectionRebuilder` interface with
`TxLifecycleProjectionRebuilderAdapter` wrapping the sealed real
implementation. `ReorgPipeline` injects it (optional, registered in
`IndexerStateSetup`) and `await`s `RebuildAsync` between the journal
append loop and the hub emit. Strict-sequence test
`OrderingPin_JournalAppendBeforeHubEmit_StrictSequence` uses a shared
`SequenceTracker` to verify every journal append sequence-number is
strictly less than every hub emit sequence-number — not just count
equality.

### H2 — explicit coinbase exclusion

**Before:** coinbase txs were skipped implicitly via the
no-raw-bytes branch.

**After:** new `ICoinbaseProbe` interface; default
`MetaTransactionCoinbaseProbe` queries
`MetaTransaction.Index == 0`. The rebroadcaster invokes the probe
BEFORE raw lookup and increments
`OrphanedTxRebroadcastRecorder.IncrementSkippedCoinbase` on hit.

### H3 — concurrent header serialization

**Before:** `HeadersChainService.HandleHeadersAsync` ran fire-and-
forget from `PeerSession.OnHeadersReceived` per peer; concurrent
header arrivals from multiple peers raced `HeadersChain` (which uses
a non-concurrent `Dictionary` by design).

**After:** `SemaphoreSlim _headersGate` (1/1) wraps
`HandleHeadersCoreAsync`. Disposed on `DisposeAsync`. Dedicated
concurrent-fork test deferred — synthetic concurrency tests against
`HeadersChain` are inherently flaky (the race surfaces as Dictionary
corruption / `InvalidOperationException`, hard to make
deterministic); the fix is mechanical and reviewable in source.

### M1 — transactional pipeline failure

**After:** `ReorgPipeline.HandleForkObservedAsync` tracks a
`progressTag` per step; any throw AFTER the first journal append
marks `BsvP2pHealth.MarkDegradedReorg` and re-throws. The journal
entries are idempotent by fingerprint, so a subsequent retry resumes
correctly.

### M3 — strict-sequence + stable-state tests

- `OrderingPin_JournalAppendBeforeHubEmit_StrictSequence` (above).
- `RepeatPlan_SameForkTip_IsIdempotent_StableState` — second
  invocation observes the now-promoted tip via the detector's
  height-equality short-circuit; no new journal append, no new hub
  event, chain tip unchanged. Stronger than the pre-revision count-
  based assertion.

### M2 — honest S6 coverage matrix

Closeout above lists every original §S6 scenario with its current
implementation status; "N/A — S2 deferred" entries are explicitly
flagged and the H3 concurrent-fork scenario is documented as a
deferred test (the implementation is in place, the test scaffolding
is not).

### L1 — DI graph regression-pin extended

`BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves` now also
asserts: `WorkBitsCumulativeWorkComparer`, `IOutgoingRawLookup`,
`ITxAnnouncer`, `ICoinbaseProbe`. (`IProjectionRebuilder` registers
in `IndexerStateSetup` next to the real rebuilder; the DI test runs
on `BsvP2pSetup` only, where `IProjectionRebuilder` is intentionally
optional.)

### L2 — doc sweep

`master.md` + `slices.md` still reference the original S2 designs
(`ReorgEventEmitter`, `P2pOrphanedBlockBodyFetcher`,
`OrphanedTxSkippedCoinbase`-via-fetched-body, etc.). These were
left as historical context for the audit chain; readers should
treat the shipped surface (`ReorgPipeline`, `RavenOrphanedTxIdReader`,
`ICoinbaseProbe`-via-MetaTransaction, etc.) as authoritative. A
"Naming drift" section is the cleanest documentation gesture; the
closeout's "Scope deviations" + "A2 revision summary" sections
together describe the difference between original-plan names and
shipped names. Future waves should reference the implementation
files, not the historical slice text.

### Final test counts after A2 revision

- `Dxs.Bsv.Tests` 220/220 (was 218 pre-A2 revision, +2:
  `Detect_LowDifficultyFork_LongerHeight_LessWork_ReturnsNull` +
  `Detect_HeightOnlyComparer_AcceptsTallerForkRegardlessOfDifficulty`).
- `Dxs.Consigliere.Tests` 341 passed (was 340; +1 net from the new
  strict-sequence `OrderingPin_JournalAppendBeforeHubEmit_StrictSequence`;
  the pre-revision `RepeatPlan_SameOrphans_FingerprintsAreIdempotent`
  was rewritten in place as
  `RepeatPlan_SameForkTip_IsIdempotent_StableState`) + 24 explicit
  Skipped + 3 pre-existing baseline Raven-runtime failures (unchanged).

## A2-followup revision summary (this commit)

A2-followup audit returned MAJOR REVISION REQUIRED with 1 critical
(persistence not restart-safe), 6 partials on the original A2 fixes,
and 2 new findings. All ten items folded.

### N1+C1+C2+M1 — persistent active-tip pointer

**Before:** `HeadersChainService.PersistAsync` wrote every header
(both `ExtendResult.Extended` and `ExtendResult.Fork`) to
`BlockHeaderStore`. On startup, `LoadFromStore` picked the highest-
height retained header as the in-memory tip. A taller-but-rejected
low-work fork survived restart and became active despite the in-
memory work-comparator having said no. C2's in-memory PromoteFork
didn't survive restart either.

**After:**

- New `BlockHeaderActiveTipDocument` (single-row, constant ID
  `block-headers/active-tip`) carrying `{ BlockHashHex (wire-order),
  Height }`.
- `IBlockHeaderStore.{Set,Get}ActiveTipAsync` interface members.
- `HeadersChainService.PersistAsync` writes the pointer ONLY on
  `ExtendResult.Extended` (and the legacy bootstrapper extension);
  the Fork case persists the header doc but DOES NOT touch the
  pointer.
- `HeadersChainService.StartAsync`, after `LoadFromStore`, reads the
  pointer and re-promotes the in-memory tip to the matching header
  via `HeadersChain.PromoteFork(activeTipHeader)`. If the pointer is
  absent (legacy data) the height-based selection is kept as the
  best-effort fallback.
- `ReorgPipeline` writes the pointer at the END of the pipeline
  (after rebuild + hub emit + notify + rebroadcast) so a mid-
  pipeline failure leaves durable state on the OLD tip. In-memory
  `PromoteFork` runs mid-pipeline (so the detector doesn't re-fire
  during the same reorg event); on a post-promote / pre-durable
  exception the catch block rolls back in-memory via
  `_chain.PromoteFork(preReorgTip)`. The journal entries are
  idempotent so the next header arrival replays the full plan
  correctly.
- Restart test pin:
  `ActiveTipPointerStartupTests.HeadersChain_PromoteFork_PromotesIfHashInRetention`
  exercises the exact startup recipe (taller fork in retention,
  active-tip pointer overrides the height-based selection).

### N2 — `MetaTransactionCoinbaseProbe` consensus check

**Before:** probe inferred coinbase from `MetaTransaction.Index == 0`.
But `TransactionStore` defaults unknown `Index` to 0, so any block
tx whose position-in-block wasn't recorded would be misclassified.

**After:** probe requires the full BSV-consensus signature
`Index == 0 AND Inputs.Count == 1 AND Inputs[0].TxId is all-zero`
(the genesis-style coinbase outpoint). A defaulted-to-zero Index
without the matching input shape no longer trips the probe.

### H2 — explicit `Coinbase_IsSkipped_NotAnnounced` test

`OrphanedTxRebroadcasterTests.Coinbase_IsSkipped_NotAnnounced` flips
`FakeCoinbaseProbe.Coinbases.Add("tx1")` and asserts: no
`announcer.Calls`, `SkippedCoinbaseCount == 1`, `AnnouncedCount == 0`,
`SkippedNoRawCount == 0`. Companion test
`NonCoinbase_FlowsThroughRawLookup_EvenWhenSomeOrphanIsCoinbase`
pins mixed lists: coinbase skipped, non-coinbase announced; the
loop doesn't short-circuit.

### M1 — full retry semantics (rollback path)

Reorder the pipeline so `SetActiveTipAsync` is the LAST step; on a
post-in-memory-promote / pre-durable failure (rebuild / hub-emit /
new-block-notify / rebroadcast steps), the catch block rolls back
the in-memory `PromoteFork` to the pre-promote tip. The detector
sees the fork again on next header arrival and retry runs through
all idempotent journal-append + hub + rebroadcast steps cleanly.

### M2 — closeout coinbase language

The "implicit/no-raw" coinbase classification line was reworded to
reflect the actual A2 H2 / A2-followup N2 explicit probe.

### L2 — inline OBSOLETE markers

`slices.md` §S2 (`P2pOrphanedBlockBodyFetcher`) and §S3
(`ReorgEventEmitter`) carry inline `<!-- OBSOLETE -->` HTML comments
plus prominent paragraph warnings at the top of each obsolete section
pointing at the shipped surface (`RavenOrphanedTxIdReader`,
`ReorgPipeline`). The counter list in §S4 was rewritten to match
the shipped `OrphanedTxRebroadcastRecorder` method names exactly.

### Final test counts after A2-followup revision

- `Dxs.Bsv.Tests` 220/220 (no Bsv-side changes).
- `Dxs.Consigliere.Tests` 346 passed (was 341 pre-A2-followup;
  +2 from `OrphanedTxRebroadcasterTests.Coinbase_*` /
  `NonCoinbase_*`, +3 from `ActiveTipPointerStartupTests`) + 24
  explicit Skipped + 3 pre-existing baseline Raven-runtime failures
  (unchanged).

## A2-followup-2 revision summary (this commit)

A2-followup-2 audit returned MAJOR REVISION REQUIRED with 3
partials and 2 new findings. All five items folded.

### N1+C1+M1 — startup walk-back + durable-failure rollback

**Before (the partial close):** the active-tip pointer existed but
`StartAsync` only seeded the in-memory chain from
`RecentAsync(retentionCount)` — top-N-by-height. If the store
retained enough taller rejected fork headers, the active tip's
ancestors got pushed out of the top-N entirely;
`TryGetByWireHashHex(activeTip.BlockHashHex)` would miss, the
warning branch fired, and the fallback was the raw-max tip — which
is exactly the rejected fork (N1 not actually closed).

**After:** `StartAsync` now does a UNION of top-N-by-height ∪
walk-back-from-active-tip. The walk-back calls
`_store.GetByHashAsync(activeTip.BlockHashHex)` and follows
`PrevHash` for up to `RetainedHeaderCount` steps. The resulting
combined set is deduped (keyed by wire-hash) and replayed via
`LoadFromStore`; the in-memory tip is then overridden via
`PromoteFork(activeTipHeader)`. The pointer-target is guaranteed
to be in the loaded set, so the height-based fallback is
unreachable in normal operation (preserved only for the degenerate
"pointer references a missing-from-store header" case which now
logs an explicit corruption warning).

For M1 (durable-commit failure): the catch block's rollback set
was widened to include `"promote-fork-durable"`. A
`SetActiveTipAsync` failure at Step 8 now rolls back the in-memory
`PromoteFork`, so the detector re-runs the plan on the next header
arrival via the still-stored fork. Without this, the system would
be wedged with in-memory tip on the new chain but durable pointer
on the old, and the detector would return null on retry.

### N3 — `HeadersSoakRecorder` spike

Spike `InMemoryBlockHeaderStore` in
`tests/Spikes/P2p/HeadersSoakRecorder/Program.cs` now implements
the new `SetActiveTipAsync` / `GetActiveTipAsync` interface members
(in-memory backing field, stubs only — the spike doesn't exercise
restart-tip semantics).

### N4 — `OrphanedTxRebroadcaster` XML doc

Replaced the "Coinbase exclusion is implicit" paragraph with the
explicit `ICoinbaseProbe` description matching the shipped code.

### M2 closeout note — test rename in row #6

The S6 coverage matrix row #6 "Idempotent replay" entry referenced
the pre-A2 test name `RepeatPlan_SameOrphans_*`; now references the
post-A2 rename `RepeatPlan_SameForkTip_IsIdempotent_StableState`
with an explanation that the post-C2 promotion makes the test
stronger (detector returns null on replay via height-equality
short-circuit, not just a duplicate journal fingerprint).

### Final test counts after A2-followup-2 revision

- `Dxs.Bsv.Tests` 220/220 (no Bsv-side changes).
- `Dxs.Consigliere.Tests` 347 passed (+1 from
  `ActiveTipWalkBack_LoadsActiveChainAncestors_DisplacedByForks`)
  + 24 explicit Skipped + 3 pre-existing baseline Raven-runtime
  failures (unchanged).
- Spike `HeadersSoakRecorder` builds clean (N3 closed).
