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
    public async Task AppendAsync_TxMessageOverload_UsesMessageTimestamp_NotUtcNow()
    {
        // Audit W4 A2-followup N1 regression: the TxMessage path was
        // unverified post-A2 H3. This test pins it.
        //
        // TxMessage.Timestamp is unix SECONDS (per BitailsRealtimeIngestRunner
        // calling .ToUnixTimeSeconds() into the factory). Build two
        // messages with the same txid: the first from P2p 5 seconds
        // ago, the second from Bitails 4 seconds ago. The lag between
        // them is 1 s → bucket 4 (1 s - 5 s).
        var tracker = new SourceVisibilityTracker();
        var writer = new TxObservationJournalWriter(
            new NoopAppender(),
            new NullPayloadStore(),
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance,
            tracker);

        var firstSeenAt = DateTimeOffset.UtcNow.AddSeconds(-5);
        var lateSeenAt = DateTimeOffset.UtcNow.AddSeconds(-4);

        // Build a TxMessage via the AddedToMempool factory used by
        // Bitails / JungleBus realtime runners.
        var tx = TestTransaction("deadbeef");
        var msgP2p = TxMessage.AddedToMempool(tx, firstSeenAt.ToUnixTimeSeconds(), TxObservationSource.P2p);
        var msgBitails = TxMessage.AddedToMempool(tx, lateSeenAt.ToUnixTimeSeconds(), TxObservationSource.Bitails);

        await writer.AppendAsync(msgP2p);
        await writer.AppendAsync(msgBitails);

        var snap = new SourceMetricsSnapshot();
        tracker.SnapshotInto(snap);

        // P2p won FirstSeen for this txid.
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
        // Bitails landed 1 second after → bucket 4 (1 s - 5 s).
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[4]);
        for (var i = 0; i < SourceMetricsBuckets.Count; i++)
            if (i != 4) Assert.Equal(0, snap.VisibilityCounters[TxObservationSource.Bitails].LagBuckets[i]);
    }

    [Fact]
    public async Task AppendAsync_TxMessageOverload_FallsBackToUtcNow_WhenTimestampIsZero()
    {
        // Edge: RemovedFromMempool factory passes default(long)=0 as
        // timestamp. The journal-writer hook must fall back to UtcNow
        // (not interpret zero as a meaningful 1970-01-01 timestamp).
        var tracker = new SourceVisibilityTracker();
        var writer = new TxObservationJournalWriter(
            new NoopAppender(),
            new NullPayloadStore(),
            Options.Create(new ConsigliereStorageConfig()),
            NullLogger<TxObservationJournalWriter>.Instance,
            tracker);

        // RemovedFromMempool uses Timestamp=default and TxId only;
        // we synthesize an equivalent via AddedToMempool with zero
        // timestamp.
        var tx = TestTransaction("cafebabe");
        var msg = TxMessage.AddedToMempool(tx, 0L, TxObservationSource.P2p);

        await writer.AppendAsync(msg);

        var snap = new SourceMetricsSnapshot();
        tracker.SnapshotInto(snap);
        // First-seen registered with UtcNow as the observed-at —
        // we can't directly assert the wall-clock value, but the
        // fact that FirstSeen incremented proves the tracker
        // received a valid timestamp.
        Assert.Equal(1, snap.VisibilityCounters[TxObservationSource.P2p].FirstSeen);
    }

    /// <summary>Build a synthetic minimal Transaction with a chosen
    /// txid suffix so tests don't all collide on the same hash.</summary>
    private static Dxs.Bsv.Models.Transaction TestTransaction(string seed)
    {
        // We just need TxMessage.AddedToMempool to succeed; the
        // transaction shape is irrelevant for the visibility-tracker
        // hook (which only reads observation.TxId after
        // TryCreateObservation). Use a minimal valid serialization.
        var raw = new System.IO.MemoryStream();
        using (var bw = new System.IO.BinaryWriter(raw, System.Text.Encoding.ASCII, leaveOpen: true))
        {
            bw.Write((uint)1);                 // version
            bw.Write((byte)0);                 // 0 inputs
            bw.Write((byte)1);                 // 1 output
            bw.Write((ulong)1000);             // 1000 sats
            // Embed `seed` bytes (max 75) in the script so each tx
            // hashes differently.
            var seedBytes = System.Text.Encoding.ASCII.GetBytes(seed);
            bw.Write((byte)seedBytes.Length);  // script length
            bw.Write(seedBytes);
            bw.Write((uint)0);                 // locktime
        }
        return Dxs.Bsv.Models.Transaction.Parse(raw.ToArray(), Dxs.Bsv.Network.Mainnet);
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
