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
| 6 | Idempotent replay | covered (`BlockObservationJournalWriterTests.AppendDisconnected_RepeatCall_*`, `ReorgPipelineTests.RepeatPlan_SameOrphans_*`) |
| 7 | Tx in OutgoingStore re-broadcast | covered (`OrphanedTxRebroadcasterTests.TxInOutgoingStore_*`) |
| 8 | Tx in PayloadStore re-broadcast | covered (`OrphanedTxRebroadcasterTests.TxInPayloadStore_*`) |
| 9 | Tx with no raw skipped | covered (`OrphanedTxRebroadcasterTests.TxWithNoRaw_*`) |
| 10 | Coinbase skipped | covered implicitly — coinbases lack raw bytes in both `OutgoingTransactionStore` and `IRawTransactionPayloadStore` (the thin-node observer doesn't fetch full bodies post S2 deferral), so they naturally fall into the `SkippedNoRaw` counter. No explicit coinbase-tagging required at the rebroadcaster |

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
- **Coinbase-explicit skip** — currently implicit via `no-raw`
  counter. If projections ever record coinbase raw bytes, the
  rebroadcaster will attempt to announce them and the peer will
  reject (counter increments). Not a correctness issue; flagged
  for the post-execution audit.
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
- [ ] `audits/wave3-audit-A2.md` — pending post-execution audit.
