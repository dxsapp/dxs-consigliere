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

    [Fact]
    public async Task RebuildAsync_DoesNotDowngradeConfirmedTxWhenLaterSequenceContainsOlderMempoolObservation()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new TxLifecycleProjectionReader(store);

        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInBlock,
                        TxObservationSource.Node,
                        "tx-2",
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_100),
                        "block-2",
                        200,
                        1
                    )
                ),
                new DedupeFingerprint("node|tx_seen_in_block|tx-2|block-2")
            )
        );
        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInMempool,
                        TxObservationSource.Node,
                        "tx-2",
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_050)
                    )
                ),
                new DedupeFingerprint("node|tx_seen_in_mempool|tx-2")
            )
        );

        await rebuilder.RebuildAsync();
        var projection = await reader.LoadAsync("tx-2");

        Assert.NotNull(projection);
        Assert.Equal(TxLifecycleStatus.Confirmed, projection.LifecycleStatus);
        Assert.True(projection.Authoritative);
        Assert.Equal("block-2", projection.BlockHash);
        Assert.Equal(200, projection.BlockHeight);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_710_000_050), projection.FirstSeenAt);
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_710_000_100), projection.LastObservedAt);
    }

    [Fact]
    public async Task RebuildAsync_RemainsDeterministicAcrossDuplicateJournalAppendAttempts()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var rebuilder = new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store));
        var reader = new TxLifecycleProjectionReader(store);
        var request = new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
            new ObservationJournalEntry<TxObservation>(
                new TxObservation(
                    TxObservationEventType.SeenInMempool,
                    TxObservationSource.Node,
                    "tx-3",
                    DateTimeOffset.FromUnixTimeSeconds(1_710_000_300)
                )
            ),
            new DedupeFingerprint("node|tx_seen_in_mempool|tx-3")
        );

        var first = await txJournal.AppendAsync(request);
        var duplicate = await txJournal.AppendAsync(request);

        var checkpoint1 = await rebuilder.RebuildAsync();
        var projection1 = await reader.LoadAsync("tx-3");

        var checkpoint2 = await rebuilder.RebuildAsync();
        var projection2 = await reader.LoadAsync("tx-3");

        Assert.False(first.IsDuplicate);
        Assert.True(duplicate.IsDuplicate);
        Assert.Equal(first.Sequence, duplicate.Sequence);
        Assert.Equal(checkpoint1, checkpoint2);
        Assert.NotNull(projection1);
        Assert.NotNull(projection2);
        Assert.Equal(projection1.LifecycleStatus, projection2.LifecycleStatus);
        Assert.Equal(projection1.LastSequence, projection2.LastSequence);
        Assert.Equal(projection1.FirstSeenAt, projection2.FirstSeenAt);
    }
}
