using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Transactions;

public class TxLifecycleProjectionRebuilderIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task RebuildAsync_ProjectsTxLifecycleAcrossMixedTxAndBlockObservations()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var blockJournal = new RavenObservationJournal<BlockObservation>(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new TxLifecycleProjectionReader(store);

        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInMempool,
                        TxObservationSource.Node,
                        "tx-1",
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_000)
                    ),
                    new RawTransactionPayloadReference("raw-tx-payloads/tx-1", "tx-1", RawTransactionPayloadCompressionAlgorithm.None)
                ),
                new DedupeFingerprint("node|tx_seen_in_mempool|tx-1")
            )
        );
        await blockJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
                new ObservationJournalEntry<BlockObservation>(
                    new BlockObservation(BlockObservationEventType.Connected, TxObservationSource.Node, "block-1")
                ),
                new DedupeFingerprint("node|block_connected|block-1")
            )
        );
        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInBlock,
                        TxObservationSource.Node,
                        "tx-1",
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_100),
                        "block-1",
                        100,
                        3
                    )
                ),
                new DedupeFingerprint("node|tx_seen_in_block|tx-1|block-1")
            )
        );
        await blockJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<BlockObservation>>(
                new ObservationJournalEntry<BlockObservation>(
                    new BlockObservation(
                        BlockObservationEventType.Disconnected,
                        TxObservationSource.Node,
                        "block-1",
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_200),
                        "orphaned"
                    )
                ),
                new DedupeFingerprint("node|block_disconnected|block-1")
            )
        );

        var checkpoint = await rebuilder.RebuildAsync();
        var projection = await reader.LoadAsync("tx-1");

        Assert.Equal(4, checkpoint.Sequence.Value);
        Assert.NotNull(projection);
        Assert.True(projection.Known);
        Assert.Equal(TxLifecycleStatus.Reorged, projection.LifecycleStatus);
        Assert.False(projection.Authoritative);
        Assert.Equal(["node"], projection.SeenBySources);
        Assert.True(projection.PayloadAvailable);
        Assert.Null(projection.BlockHash);
        Assert.Null(projection.BlockHeight);
        Assert.Null(projection.SeenInMempool);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_710_000_000), projection.FirstSeenAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_710_000_200), projection.LastObservedAt);
    }
}
