---
created: 2026-05-17
type: slices
parent: reorg-handling-wave
status: draft
---

# Wave 3 — Slice Details

Cross-references in this file:

- `master.md` — wave goal, scope, ownership zones, slice ledger.
- W1: `docs/stream-tasks/bsv-headers-chain-wave/master.md`
  (frozen `IBlockHeaderStore`, `BlockHeader`, `HeadersChain`,
  `IWalletHub.OnReorg`, `ReorgEventDto`).
- W2: `docs/stream-tasks/bsv-mempool-observer-wave/master.md`
  (delivered `TxRelayCoordinator.AnnounceAsync`,
  `BsvP2pHealth`, `PerSessionDispatcherRegistry`,
  `IRawTransactionPayloadStore`).

## S0 — Block journal contract extension (prerequisite)

This slice mirrors W2 S0 (tx journal source-neutral overload).
It extends the existing
`src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs`
with a `Disconnected` append path so that downstream
`TxLifecycleProjectionRebuilder.ApplyBlockObservationAsync` can
react. The slice has its own audit (`audits/S0-A1.md`) before
S1-S7 may open.

### S0.1 — `BlockObservationSource` constant set

Add (or create) `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservationSource.cs`:

```csharp
namespace Dxs.Bsv.BitcoinMonitor.Models;

public static class BlockObservationSource
{
    public const string Node = "node";
    public const string JungleBus = "junglebus";
    public const string Reorg = "reorg";    // W3 — fork-detected disconnects
}
```

Rationale: `BlockObservation.Source` is already a free-form
string; W3 just stabilises the values so downstream code can
discriminate. Parallel to `TxObservationSource.P2p` from W2 S0.
Existing call sites of `AppendConnectedAsync` continue to pass
their natural source string (mostly `"node"` /
`"junglebus"`); W3 adds `"reorg"` to that allowed-values list.

### S0.2 — `AppendDisconnectedAsync` overload

Add to
`src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs`:

```csharp
public async Task<bool> AppendDisconnectedAsync(
    string blockHash,
    string source,
    string? reason = null,
    CancellationToken cancellationToken = default)
{
    if (string.IsNullOrEmpty(blockHash)) return false;
    if (string.IsNullOrEmpty(source))    return false;

    var observation = new BlockObservation(
        EventType:   BlockObservationEventType.Disconnected,
        Source:      source,
        BlockHash:   blockHash,
        ObservedAt:  DateTimeOffset.UtcNow,
        Reason:      reason);

    var fingerprint = $"block.disconnected:{blockHash}:{source}";

    var request = new ObservationJournalAppendRequest<
        ObservationJournalEntry<BlockObservation>>(
        new ObservationJournalEntry<BlockObservation>(observation, null),
        new DedupeFingerprint(fingerprint));

    var result = await _appender.AppendAsync(request, cancellationToken);
    return !result.IsDuplicate;
}
```

- The dedupe-fingerprint format `block.disconnected:{blockHash}:{source}`
  makes the call idempotent per
  `(blockHash, source)`. A reorg detected twice from the same
  source on the same orphan block lands in the journal once.
- Return type matches W2 S0 (`bool` true = newly appended,
  false = duplicate). Audit S0-A1 H1 fix (IsDuplicate
  propagation) is a hard requirement here.
- The existing `AppendConnectedAsync` continues to work; W3
  does NOT migrate the connected path. The two methods share
  the same appender.

### S0.3 — Tests

Add `tests/Dxs.Consigliere.Tests/BackgroundTasks/Blocks/BlockObservationJournalWriterTests.cs`:

- `AppendDisconnected_FirstCall_ReturnsTrue` — captures a fresh
  append on a capturing appender.
- `AppendDisconnected_RepeatCall_SameFingerprint_ReturnsFalse` —
  capturing appender mocks `IsDuplicate = true` on the second
  call; method returns false.
- `AppendDisconnected_EmptyBlockHash_ReturnsFalse` — guard.
- `AppendDisconnected_EmptySource_ReturnsFalse` — guard.
- `AppendDisconnected_FingerprintIncludesBlockHashAndSource` —
  inspect the captured request's dedupe-fingerprint and assert
  the format. Pins the contract for future emitters.

### Owned paths (S0)

- `src/Dxs.Bsv/BitcoinMonitor/Models/BlockObservationSource.cs` (new)
- `src/Dxs.Consigliere/BackgroundTasks/Blocks/BlockObservationJournalWriter.cs` (edit — add method)
- `tests/Dxs.Consigliere.Tests/BackgroundTasks/Blocks/BlockObservationJournalWriterTests.cs` (new)

### Validation (S0)

- `dotnet build Dxs.Consigliere.sln -c Release` returns 0 errors.
- `dotnet test --filter FullyQualifiedName~BlockObservationJournalWriterTests` green.
- `audits/S0-A1.md` returns APPROVE before S1+ open.

## S1 — `ReorgDetector` pure logic

`src/Dxs.Bsv/P2p/Chain/ReorgDetector.cs`. Stateless pure logic
called by `HeadersChainService` (existing W1 service) whenever a
`Fork(header, parentHeight)` result is observed.

### Inputs

- The fork-side header chain (the new chain that built on a
  non-tip ancestor). Reconstructed by walking back from the
  fork tip through `IBlockHeaderStore.GetByHashAsync` until we
  hit an ancestor that is also on the active chain.
- The active-chain tip from `IBlockHeaderStore.GetTipAsync`.
- The retention window
  (`HeadersChainOptions.RetainedHeaderCount`, default 200).

### Output

```csharp
public sealed record ReorgPlan(
    string CommonAncestorHash,
    long   CommonAncestorHeight,
    IReadOnlyList<string> OrphanedHashes,  // active-chain blocks
                                           // from common-ancestor+1
                                           // up to old tip, in
                                           // disconnect order
                                           // (newest first)
    IReadOnlyList<string> NewChainHashes,  // fork-side blocks
                                           // from common-ancestor+1
                                           // up to new tip, in
                                           // connect order
                                           // (oldest first)
    string NewTipHash,
    long   NewTipHeight,
    bool   IsDegraded);
```

`IsDegraded` is `true` when `CommonAncestorHeight < tipHeight -
RetainedHeaderCount` — i.e. we don't have the orphaned headers in
the retention window, so we can't enumerate `OrphanedHashes`
reliably. In that case `OrphanedHashes = []` and
`NewChainHashes = []`; the emitter fires a single
`OnReorg(DegradedState = true)` and stops.

### Core rule (Core Rule §4)

Fork-promotion comparison uses **height** as a proxy for
cumulative work in this slice. BSV difficulty is roughly stable
across a 200-header window, so equal cumulative-work-per-block.
The comparator is `ICumulativeWorkComparer` (interface) with one
default implementation `HeightCumulativeWorkComparer` so a future
wave can swap in a real work comparator without breaking the
detector's surface. The detector takes the comparer as a
constructor dependency.

### Equal-height tie-break

When the fork side equals the active tip in height, **the active
chain wins** (first-seen). This is BSV's first-seen rule. The
detector returns `null` (no plan) in that case.

### Validation (S1)

- `tests/Dxs.Bsv.Tests/P2p/Chain/ReorgDetectorTests.cs`:
  - `Detector_NoFork_ReturnsNull`
  - `Detector_EqualHeight_FirstSeenWins_ReturnsNull`
  - `Detector_OneDeepFork_ReturnsPlan_WithCorrectOrphans`
  - `Detector_TwoDeepFork_ReturnsPlan_WithCorrectOrphans`
  - `Detector_FiveDeepFork_ReturnsPlan`
  - `Detector_ForkPointBelowRetention_ReturnsDegraded`
  - `Detector_OrphanedHashes_AreInDisconnectOrder_NewestFirst`
  - `Detector_NewChainHashes_AreInConnectOrder_OldestFirst`

All unit tests; no I/O — feed the detector a fake
`IBlockHeaderStore` and assert the returned plan.

### Owned paths (S1)

- `src/Dxs.Bsv/P2p/Chain/{ReorgDetector,ReorgPlan,ReorgDetectorOptions,ICumulativeWorkComparer,HeightCumulativeWorkComparer}.cs` (new)
- `tests/Dxs.Bsv.Tests/P2p/Chain/ReorgDetectorTests.cs` (new)

## S2 — `P2pOrphanedBlockBodyFetcher` <!-- OBSOLETE: deferred mid-wave -->

> **OBSOLETE — DO NOT IMPLEMENT.** The block-body fetch over P2P was
> deferred mid-wave (BSV mainnet blocks are GB-scale; the projection's
> `BlockHash` index is sufficient). See `evidence/closeout.md` §"Scope
> deviations §S2". The shipped substitute is
> `RavenOrphanedTxIdReader` (queries projections directly).
> The text below is retained as historical context for the audit chain
> only.

`src/Dxs.Consigliere/Services/P2p/P2pOrphanedBlockBodyFetcher.cs`
implementing
`src/Dxs.Bsv/P2p/Chain/IOrphanedBlockBodyFetcher.cs`:

```csharp
public interface IOrphanedBlockBodyFetcher
{
    Task<OrphanedBlockBody?> FetchAsync(
        string blockHashDisplayHex,
        CancellationToken cancellationToken);
}

public sealed record OrphanedBlockBody(
    BlockHeader Header,
    IReadOnlyList<string> TxIds);  // display-order, including coinbase[0]
```

### Behaviour

1. Resolve a Ready peer from
   `BsvP2pHealth.ActiveSessions.Where(s => s.State == Ready)`.
   If none, return `null`.
2. Subscribe to the per-session dispatcher
   (`PerSessionDispatcherRegistry.For(session).Subscribe(
   P2pCommands.Block, tag: "reorg.block-fetch.{blockHash[..8]}",
   handler)`) for one `block` message.
3. Send `getdata(MSG_BLOCK, wireOrderHash)` via
   `session.SendGetDataAsync(...)`.
4. Await the `block` frame or the `BlockFetchTimeoutMs`
   timeout. On timeout, retry on the next Ready peer (up to
   `MaxBlockFetchRetries`, default 3).
5. On receive: parse the block payload (header + tx list);
   validate `block.payload_length <= MaxFetchedBlockBytes` (cap
   defaults to 256 MiB; oversize → reject + counter increment).
6. Compute merkle root from the parsed tx list (BSV uses
   double-SHA256 pairwise; existing utility under
   `Dxs.Bsv/Hashing/`); compare with `header.MerkleRoot`.
   Mismatch → reject + counter increment + retry next peer.
7. Return `OrphanedBlockBody(header, txIdsDisplayOrder)`.

### Caps + counters

- `MaxFetchedBlockBytes` — default 256 MiB. Configured under
  `Consigliere:Broadcast:P2p:MaxFetchedBlockBytes` in
  `BsvP2pConfig`. Startup warning if < 32 MiB (mainnet blocks
  routinely exceed 32 MiB in 2026).
- `BlockFetchTimeoutMs` — default 60 000.
- `MaxBlockFetchRetries` — default 3.
- Counter `OrphanedBlockFetchSucceeded`,
  `OrphanedBlockFetchTimeout`,
  `OrphanedBlockFetchMerkleMismatch`,
  `OrphanedBlockFetchOversize`,
  `OrphanedBlockFetchNoReadyPeer`.

### Validation (S2)

`tests/Dxs.Consigliere.Tests/P2p/Reorg/P2pOrphanedBlockBodyFetcherTests.cs`:

- `Fetch_ValidBody_ReturnsTxIds_InDisplayOrder`
- `Fetch_MismatchedMerkleRoot_RejectsAndRetriesNextPeer`
- `Fetch_OversizeBody_RejectedWithCounter`
- `Fetch_Timeout_RetriesNextPeer_UpToMax`
- `Fetch_NoReadyPeer_ReturnsNull_WithCounter`
- `Fetch_ThreeFailedRetries_ReturnsNull`

All driven through `MiniBsvServer` — server hand-crafts the
block payload to control merkle validity / size / timeout.

### Owned paths (S2)

- `src/Dxs.Bsv/P2p/Chain/IOrphanedBlockBodyFetcher.cs` (new — interface + record)
- `src/Dxs.Consigliere/Services/P2p/P2pOrphanedBlockBodyFetcher.cs` (new)
- `src/Dxs.Consigliere/Configs/BsvP2pConfig.cs` (extend — fetch caps)
- `tests/Dxs.Consigliere.Tests/P2p/Reorg/P2pOrphanedBlockBodyFetcherTests.cs` (new)

## S3 — `ReorgEventEmitter` hosted service <!-- OBSOLETE: renamed -->

> **OBSOLETE — DO NOT IMPLEMENT.** Shipped as `ReorgPipeline` (not a
> hosted service; invoked synchronously by `HeadersChainService.case
> ExtendResult.Fork`). The orchestration shape was updated per the
> A2 + A2-followup revisions: detector → enumerate orphan txids →
> journal-append → in-memory `HeadersChain.PromoteFork` → projection
> rebuild → `OnReorg` hub emit → `OnNewBlock` notify → rebroadcast
> → durable `SetActiveTipAsync` (last). The original
> `IOrphanedBlockBodyFetcher` dependency was dropped (see S2 above);
> the projection-query path via `IOrphanedTxIdReader` replaced it.
> Text below is historical.

`src/Dxs.Consigliere/Services/P2p/ReorgEventEmitter.cs`. Hosted
service that drives the end-to-end reorg pipeline:

```csharp
public sealed class ReorgEventEmitter : IHostedService
{
    public async Task HandleReorgPlanAsync(
        ReorgPlan plan,
        CancellationToken cancellationToken)
    {
        if (plan.IsDegraded)
        {
            _health.MarkDegradedReorg(DateTimeOffset.UtcNow);
            await _hub.Clients
                .Group("block:tip")
                .OnReorg(new ReorgEventDto(
                    CommonAncestorHash:   plan.CommonAncestorHash,
                    CommonAncestorHeight: plan.CommonAncestorHeight,
                    OrphanedHashes:       Array.Empty<string>(),
                    NewTipHash:           plan.NewTipHash,
                    NewTipHeight:         plan.NewTipHeight,
                    DegradedState:        true));
            return;
        }

        // 1. Fetch every orphan body (newest-first, the
        //    disconnect order). Skip the rest if any fetch
        //    returns null after retries — partial application
        //    would corrupt projection state.
        var fetched = new List<OrphanedBlockBody>(plan.OrphanedHashes.Count);
        foreach (var orphanHash in plan.OrphanedHashes)
        {
            var body = await _fetcher.FetchAsync(orphanHash, cancellationToken);
            if (body is null) { _recorder.IncrementReorgAbandoned(); return; }
            fetched.Add(body);
        }

        // 2. Journal-append every Disconnected event (idempotent
        //    by fingerprint).
        foreach (var orphanHash in plan.OrphanedHashes)
        {
            await _journal.AppendDisconnectedAsync(
                orphanHash,
                BlockObservationSource.Reorg,
                reason: $"fork:{plan.CommonAncestorHash}",
                cancellationToken);
        }

        // 3. Fire hub event.
        await _hub.Clients
            .Group("block:tip")
            .OnReorg(new ReorgEventDto(
                CommonAncestorHash:   plan.CommonAncestorHash,
                CommonAncestorHeight: plan.CommonAncestorHeight,
                OrphanedHashes:       plan.OrphanedHashes.ToArray(),
                NewTipHash:           plan.NewTipHash,
                NewTipHeight:         plan.NewTipHeight,
                DegradedState:        false));

        // 4. Re-broadcast pass (S4).
        await _rebroadcaster.RebroadcastAsync(fetched, cancellationToken);
    }
}
```

The hosted-service `StartAsync` does NOT poll headers; instead,
`HeadersChainService` (W1) is extended in S5 with one optional
callback `OnReorgDetected(ReorgPlan)` that the emitter handler
above implements. `HeadersChainService` calls
`ReorgDetector.TryDetect(...)` after every `ExtendResult.Fork`
event and invokes the callback when a plan is produced.

### Ordering rule (Core Rule §9)

Journal append happens before hub emit — clients reacting to the
hub event by re-querying projections must observe `Reorged`
state. The journal-replay pipeline is in-process and runs
synchronously enough that by the time `OnReorg` fires the
rebuilder will have caught up. Pinned by the S6 integration
test (`Reorg_HubEvent_FiresAfterProjectionsAreReorged`).

### Validation (S3)

`tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgEventEmitterIntegrationTests.cs`:

- `OnePlan_AppendsDisconnectedForEachOrphan_InOrder`
- `OnePlan_FiresOnReorg_WithCorrectDto`
- `DegradedPlan_FiresSingleOnReorg_WithDegradedTrue_NoJournalAppend`
- `DegradedPlan_UpdatesHealthLastDegradedReorgAt`
- `FetchFailure_AbortsPlan_NoJournalAppend_NoHubEvent`
- `RepeatPlan_SameOrphans_JournalReturnsDuplicate_HubEventFiresOnceWithIsDuplicateFlag`
- `Reorg_HubEventFiresAfterJournalAppend` (ordering pin)

### Owned paths (S3)

- `src/Dxs.Consigliere/Services/P2p/ReorgEventEmitter.cs` (new)
- `src/Dxs.Consigliere/Services/P2p/BsvP2pHealth.cs` (edit — add `LastDegradedReorgAt` + `MarkDegradedReorg(...)`)
- `src/Dxs.Consigliere/Services/P2p/HeadersChainService.cs` (edit — call detector on `Fork` events; invoke emitter callback) [may be a different filename in actual code; locate the W1 chain service]
- `tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgEventEmitterIntegrationTests.cs` (new)

## S4 — `OrphanedTxRebroadcaster`

`src/Dxs.Consigliere/Services/P2p/OrphanedTxRebroadcaster.cs`:

```csharp
public async Task RebroadcastAsync(
    IReadOnlyList<OrphanedBlockBody> fetchedOrphans,
    CancellationToken cancellationToken)
{
    foreach (var body in fetchedOrphans)
    {
        for (var i = 0; i < body.TxIds.Count; i++)
        {
            var txid = body.TxIds[i];

            // Core Rule §6: coinbase is block-bound.
            if (i == 0) { _recorder.IncrementCoinbaseSkipped(); continue; }

            var rawHex = await ResolveRawHexAsync(txid, cancellationToken);
            if (rawHex is null) { _recorder.IncrementNoRaw(); continue; }

            try
            {
                var announced = await _txRelay.AnnounceAsync(
                    txid, rawHex, cancellationToken);
                if (announced > 0)
                    _recorder.IncrementAnnounced();
                else
                    _recorder.IncrementAnnounceNoReadyPeer();
            }
            catch (Exception ex)
            {
                _recorder.IncrementAnnounceFailed();
                _logger.LogWarning(ex,
                    "re-broadcast announce failed for tx {TxId}", txid);
            }
        }
    }
}

private async Task<string?> ResolveRawHexAsync(
    string txid, CancellationToken ct)
{
    var outgoing = await _outgoingStore.GetOrNullAsync(txid, ct);
    if (outgoing?.RawHex is { Length: > 0 } x) return x;

    var payload = await _payloadStore.LoadByTxIdAsync(txid, ct);
    if (payload?.PayloadHex is { Length: > 0 } y) return y;

    return null;
}
```

### `OrphanedTxRebroadcastRecorder`

Mirror `SourceObservationRecorder` (W2 S4): one
`Interlocked.Increment` per counter, expose `GetXCount()`
readers.

Counters (as shipped — names match the actual
`OrphanedTxRebroadcastRecorder` methods after the A2 + A2-followup
revisions):

- `Announced` (`IncrementAnnounced` / `GetAnnouncedCount`)
- `SkippedCoinbase` (`IncrementSkippedCoinbase` / `GetSkippedCoinbaseCount`,
  added in A2 H2)
- `SkippedNoRaw` (`IncrementSkippedNoRaw` / `GetSkippedNoRawCount`)
- `AnnounceNoReadyPeer` (`IncrementAnnounceNoReadyPeer` /
  `GetAnnounceNoReadyPeerCount`)
- `AnnounceFailed` (`IncrementAnnounceFailed` / `GetAnnounceFailedCount`)

### Validation (S4)

`tests/Dxs.Consigliere.Tests/P2p/Reorg/OrphanedTxRebroadcasterTests.cs`:

- `Coinbase_IsSkipped_NotAnnounced`
- `TxInOutgoingStore_IsAnnounced_FromOutgoingRaw`
- `TxInPayloadStore_IsAnnounced_FromPayloadRaw`
- `OutgoingTakesPrecedenceOverPayload`
- `MissingRaw_IncrementsNoRaw_NotAnnounced`
- `AnnounceFailure_IncrementsFailed_DoesNotThrow`
- `EmptyOrphanList_DoesNothing`
- `MultipleOrphans_AnnouncesEachNonCoinbase`

### Owned paths (S4)

- `src/Dxs.Consigliere/Services/P2p/{OrphanedTxRebroadcaster,OrphanedTxRebroadcastRecorder}.cs` (new)
- `tests/Dxs.Consigliere.Tests/P2p/Reorg/OrphanedTxRebroadcasterTests.cs` (new)

## S5 — DI + hosted-service wiring

`src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` extension. Mirrors
the W2 pattern that registers
`PerSessionDispatcherRegistry` + `MempoolWatcher` +
`P2pMempoolIngestRunner`.

```csharp
services.AddSingleton<HeightCumulativeWorkComparer>();
services.AddSingleton<ICumulativeWorkComparer>(sp =>
    sp.GetRequiredService<HeightCumulativeWorkComparer>());
services.AddSingleton<ReorgDetector>();
services.AddSingleton<IOrphanedBlockBodyFetcher,
    P2pOrphanedBlockBodyFetcher>();
services.AddSingleton<OrphanedTxRebroadcastRecorder>();
services.AddSingleton<OrphanedTxRebroadcaster>();
services.AddSingleton<ReorgEventEmitter>();
services.AddHostedService(sp =>
    sp.GetRequiredService<ReorgEventEmitter>());
```

### DI regression test (S5 sub-slice)

Extend
`tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests.cs`
with a new test
`W3_SingletonGraph_Resolves` that resolves every W3 singleton
from a mocked-external-deps service-provider — same approach as
the W2 A2-C1 fix that turned production-DI-graph drift into a
build failure instead of a host-startup crash.

### Validation (S5)

- New DI test compiles + green.
- Production app still builds + starts in the dev environment.
- `BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves` is
  the canonical proof.

### Owned paths (S5)

- `src/Dxs.Consigliere/Setup/BsvP2pSetup.cs` (edit — add S2-S5 registrations)
- `tests/Dxs.Consigliere.Tests/Setup/BsvP2pSetupDiResolutionTests.cs` (extend)

## S6 — End-to-end fixture suite

`tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgEndToEndFixtureTests.cs`.

Each fixture starts a `MiniBsvServer`, builds an active chain of
N+1 headers via `HeadersChainService` (W1), then has the server
advertise an alternate chain that promotes by some delta. The
fixture asserts:

- (a) The journal contains `BlockObservation(Disconnected)`
  entries for each expected orphan, in disconnect order.
- (b) `TxLifecycleProjectionDocument` rows for tx that lived in
  the orphaned blocks (seeded by the fixture before the reorg)
  show `LifecycleStatus = Reorged`.
- (c) The capturing hub client received exactly one `OnReorg`
  call with the expected DTO payload.
- (d) The mini server received `inv(MSG_TX)` frames for each
  non-coinbase orphan tx with raw bytes available.

### Scenarios

| # | Scenario | Active depth | Fork depth | Expected |
|---|---|---|---|---|
| 1 | Single-block reorg | tip = H | tip+1 = H' | 1 orphan; 1 OnReorg; non-degraded |
| 2 | Two-block reorg | H..H+1 | H..H+2 | 2 orphans (newest-first disconnect); 1 OnReorg |
| 3 | Five-block reorg | H..H+4 | H..H+5 | 5 orphans; 1 OnReorg |
| 4 | Deep reorg beyond window | fork point @ height (tip - 210) | (tip - 210)..tip+1 | 0 orphans; 1 OnReorg(DegradedState=true); no journal append |
| 5 | Mismatched-body | tip = H | tip+1 = H' | fetcher rejects body twice (different peers); plan abandoned; no journal; no hub event; counter `OrphanedBlockFetchMerkleMismatch >= 2` |
| 6 | Idempotent replay | tip = H | tip+1 = H' | trigger plan handling twice in succession; second call appends `IsDuplicate = true`; one hub event total (the second `OnReorg` is suppressed when every append is duplicate) OR two hub events with second-DegradedState=false — wave audit to pick one; default: emit both (clients receive duplicate OnReorg and de-dupe client-side) |
| 7 | Tx in OutgoingStore re-broadcast | tip = H w/ tx X | tip+1 = H' | OnReorg fires; `inv(X)` observed at server; recorder `OrphanedTxAnnounced == 1` |
| 8 | Tx in PayloadStore re-broadcast | as 7 but X only in payload store | as 7 | as 7 |
| 9 | Tx with no raw skipped | as 7 but X has no raw anywhere | as 7 | no `inv(X)`; `OrphanedTxSkippedNoRaw == 1` |
| 10 | Coinbase skipped | as 7 | as 7 | no `inv(coinbase)`; `OrphanedTxSkippedCoinbase == 1` |

### Reproducibility block

The fixtures construct blocks deterministically: fixed
timestamps, fixed difficulty target, scripted nonces. Block
serialisation reuses `Dxs.Bsv` helpers. The fixtures must
produce the same merkle roots on every run.

### Bench (`evidence/reorg-bench.md`)

A 100-reorg loop drives the emitter through `MiniBsvServer`
and reports `reorgs/min` throughput. Threshold ≥ 100/min (loose
— reorgs are rare in reality, but the throughput proves we're
not gated on synchronization).

### Owned paths (S6)

- `tests/Dxs.Consigliere.Tests/P2p/Reorg/ReorgEndToEndFixtureTests.cs` (new)
- `tests/Dxs.Consigliere.Benchmarks/ReorgEmitterThroughputBench.cs` (new — xunit + Stopwatch, mirrors W2 watchlist-bench pattern; writes `evidence/reorg-bench.md`)
- `docs/stream-tasks/reorg-handling-wave/evidence/reorg-bench.md` (generated)

## S7 — Live mainnet validation (operator-driven)

Optional, deferred per W1 / W2 pattern.

### Procedure

1. Operator runs Consigliere connected to BSV mainnet with a
   capturing SignalR client on the `block:tip` group.
2. Wait for a natural 1-deep reorg (multiple per day on BSV).
3. Confirm: `OnReorg` fires within 10 s of the new tip headers
   arriving; affected `TxLifecycleProjectionDocument` rows show
   `LifecycleStatus = Reorged`; counter `OrphanedTxAnnounced
   >= 1` if any affected txs had raw bytes.

### Required evidence schema (`evidence/live-validation.md`)

- `reorg.timestamp_utc`
- `reorg.common_ancestor_hash`
- `reorg.common_ancestor_height`
- `reorg.orphaned_count`
- `reorg.new_tip_hash`
- `reorg.new_tip_height`
- `reorg.degraded_state`
- `hub.on_reorg.received_at_utc` (capturing client)
- `hub.delta_ms`
- `projections.reorged_count`
- `rebroadcast.announced_count`
- `rebroadcast.skipped_coinbase_count`
- `rebroadcast.skipped_no_raw_count`

### Pass condition

One observed live-mainnet reorg with all evidence fields
recorded. May be operator-deferred to a post-wave session;
record the deferral in `evidence/closeout.md`.

## Dependency Graph

```
S0 (block journal contract extension)
 ├──> S1 (ReorgDetector pure)
 │     └──> S3 (ReorgEventEmitter) ──> S4 (Rebroadcaster) ──> S6 (E2E)
 ├──> S2 (BlockBodyFetcher) ────────────^                       ^
 └─────────────────────────────────────────> S5 (DI wiring) ──> S6
S6 (E2E suite) ────> S7 (live validation, operator-driven)
```

## Per-slice Audit Rules

- S0 has its own slice-level audit (`audits/S0-A1.md`) gating
  S1+ open. Same pattern as W2 S0.
- S1-S6 covered by the single wave-level audit at
  `audits/wave3-audit-A1.md` after all close.
- S7 may close after the wave audit if operator-deferred — same
  pattern as W2 S8.
- Post-execution audit `audits/wave3-audit-A2.md` runs after S6
  closes (and before W4 opens) as required by the program rule
  "every wave audited twice — pre + post".

## Validation Matrix (wave-level)

| Validation | Source | Owned by | Pin |
|---|---|---|---|
| Build green | `dotnet build` | every slice | CI |
| Tests green | `dotnet test` | every slice | CI |
| Journal idempotency | S0 unit tests + S6.6 fixture | S0 / S6 | `BlockObservationJournalWriterTests.AppendDisconnected_RepeatCall_SameFingerprint_ReturnsFalse` + `ReorgEndToEndFixtureTests.Reorg_RepeatPlan_IsIdempotent` |
| Detector correctness | S1 unit tests | S1 | `ReorgDetectorTests.*` |
| Block body validation | S2 integration tests + S6.5 fixture | S2 / S6 | `P2pOrphanedBlockBodyFetcherTests.*` |
| Emitter ordering | S3 integration test + S6.* | S3 / S6 | `ReorgEventEmitterIntegrationTests.Reorg_HubEventFiresAfterJournalAppend` |
| Re-broadcast counters | S4 unit tests | S4 | `OrphanedTxRebroadcasterTests.*` |
| DI graph | S5 regression test | S5 | `BsvP2pSetupDiResolutionTests.W3_SingletonGraph_Resolves` |
| E2E reorg | S6 fixture suite | S6 | `ReorgEndToEndFixtureTests.*` |
| Live mainnet | S7 operator session | S7 | `evidence/live-validation.md` |
