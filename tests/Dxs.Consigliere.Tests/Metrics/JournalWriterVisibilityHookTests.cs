using System;
using System.Threading;
using System.Threading.Tasks;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Metrics;
using Dxs.Consigliere.Services.Metrics;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Metrics;

/// <summary>
/// Wave 4 A2 H3 regression: the journal-writer's tracker hook must
/// pass the SOURCE's own observed-at timestamp (TxMessage.Timestamp
/// or TxObservation.ObservedAt), not <c>DateTimeOffset.UtcNow</c>.
/// Otherwise the lag histogram measures local journal-fetch latency
/// rather than cross-source first-seen timing.
/// </summary>
public class JournalWriterVisibilityHookTests
{
    private sealed class NoopAppender : IObservationJournalAppender<ObservationJournalEntry<TxObservation>>
    {
        public ValueTask<ObservationJournalAppendResult> AppendAsync(
            ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>> request,
            CancellationToken ct = default)
            => ValueTask.FromResult(new ObservationJournalAppendResult(new JournalSequence(1), false));
    }

    private sealed class NullPayloadStore : IRawTransactionPayloadStore
    {
        public Task<RawTransactionPayloadReference> SaveAsync(
            string txId, string payloadHex, string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
            CancellationToken ct = default) =>
            Task.FromResult<RawTransactionPayloadReference>(null!);
        public Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(string txId, CancellationToken ct = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null!);
        public Task<RawTransactionPayloadEnvelope> LoadAsync(RawTransactionPayloadReference r, CancellationToken ct = default)
            => Task.FromResult<RawTransactionPayloadEnvelope>(null!);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutralOverload_UsesObservationObservedAt_NotUtcNow()
    {
        var tracker = new SourceVisibilityTracker();
        var writer = new TxObservationJournalWriter(
            new NoopAppender(),
            new NullPayloadStore(),
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance,
            tracker);

        // Construct a TxObservation with an ObservedAt that is
        // materially different from current UtcNow (3 minutes ago).
        var fakeObservedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        var observation = new TxObservation(
            EventType: TxObservationEventType.SeenInMempool,
            Source: TxObservationSource.P2p,
            TxId: "aabbccdd",
            ObservedAt: fakeObservedAt);

        await writer.AppendAsync(observation, payload: null, TxObservationSource.P2p);

        // Second source observes 100 ms later — the lag should be
        // measured from fakeObservedAt (=> 100 ms = bucket 2), NOT
        // from now-3min (which would push the lag to bucket 5 >5s).
        tracker.RecordObservation("aabbccdd", TxObservationSource.Bitails,
            fakeObservedAt.AddMilliseconds(100));

        var snap = new SourceMetricsSnapshot();
        tracker.SnapshotInto(snap);
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[2]);
        // Definitely NOT in any other bucket.
        for (var i = 0; i < SourceMetricsBuckets.Count; i++)
            if (i != 2) Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[i]);
    }

    [Fact]
    public async Task AppendAsync_SourceNeutralOverload_FallsBackToUtcNow_WhenObservedAtNull()
    {
        var tracker = new SourceVisibilityTracker();
        var writer = new TxObservationJournalWriter(
            new NoopAppender(),
            new NullPayloadStore(),
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance,
            tracker);

        // ObservedAt == null → fall back to UtcNow.
        var observation = new TxObservation(
            EventType: TxObservationEventType.SeenInMempool,
            Source: TxObservationSource.P2p,
            TxId: "abc",
            ObservedAt: null);

        await writer.AppendAsync(observation, payload: null, TxObservationSource.P2p);

        // Tracker recorded FirstSeen for P2p.
        var snap = new SourceMetricsSnapshot();
        tracker.SnapshotInto(snap);
        Assert.True(snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen >= 1);
    }
}
