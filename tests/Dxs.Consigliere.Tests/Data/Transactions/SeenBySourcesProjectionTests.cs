using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Transactions;

/// <summary>
/// Wave 2 S0 projection-rebuild assertion: when the journal replay
/// contains the same txid observed from multiple sources, the
/// resulting <see cref="TxLifecycleProjectionDocument.SeenBySources"/>
/// must accumulate both source tags (order-independent).
/// </summary>
public class SeenBySourcesProjectionTests : RavenTestDriver
{
    [SkippableFact]
    public async Task RebuildAsync_AccumulatesP2pAndBitailsForSameTxid()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new TxLifecycleProjectionReader(store);

        const string txId = "tx-multi-source";
        var seenAt = DateTimeOffset.FromUnixTimeSeconds(1_710_000_000);

        // 1. P2P observer sees the tx first
        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInMempool,
                        TxObservationSource.P2p,
                        txId,
                        seenAt
                    )
                ),
                new DedupeFingerprint($"{TxObservationSource.P2p}|{TxObservationEventType.SeenInMempool}|{txId}")
            )
        );

        // 2. Bitails realtime sees the same tx shortly after
        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInMempool,
                        TxObservationSource.Bitails,
                        txId,
                        seenAt.AddSeconds(1)
                    )
                ),
                new DedupeFingerprint($"{TxObservationSource.Bitails}|{TxObservationEventType.SeenInMempool}|{txId}")
            )
        );

        var checkpoint = await rebuilder.RebuildAsync();
        var projection = await reader.LoadAsync(txId);

        Assert.Equal(2, checkpoint.Sequence.Value);
        Assert.NotNull(projection);
        Assert.True(projection.Known);

        // SeenBySources accumulates both tags, order-independent
        Assert.Contains(TxObservationSource.P2p, projection.SeenBySources);
        Assert.Contains(TxObservationSource.Bitails, projection.SeenBySources);
        Assert.Equal(2, projection.SeenBySources.Length);
    }

    [SkippableFact]
    public async Task RebuildAsync_AccumulatesP2pBitailsJungleBus_OrderIndependent()
    {
        Skip.IfNot(DotNetRuntimeFacts.HasRuntimeMajor(8), "embedded Raven .NET 8 runtime not available locally");

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new TxLifecycleProjectionReader(store);

        const string txId = "tx-three-source";
        var sources = new[]
        {
            TxObservationSource.JungleBus,
            TxObservationSource.P2p,
            TxObservationSource.Bitails,
        };

        for (var i = 0; i < sources.Length; i++)
        {
            var source = sources[i];
            await txJournal.AppendAsync(
                new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                    new ObservationJournalEntry<TxObservation>(
                        new TxObservation(
                            TxObservationEventType.SeenInMempool,
                            source,
                            txId,
                            DateTimeOffset.FromUnixTimeSeconds(1_710_000_000 + i)
                        )
                    ),
                    new DedupeFingerprint($"{source}|{TxObservationEventType.SeenInMempool}|{txId}")
                )
            );
        }

        await rebuilder.RebuildAsync();
        var projection = await reader.LoadAsync(txId);

        Assert.NotNull(projection);
        Assert.Equal(3, projection.SeenBySources.Length);
        Assert.Contains(TxObservationSource.P2p, projection.SeenBySources);
        Assert.Contains(TxObservationSource.Bitails, projection.SeenBySources);
        Assert.Contains(TxObservationSource.JungleBus, projection.SeenBySources);
    }
}
