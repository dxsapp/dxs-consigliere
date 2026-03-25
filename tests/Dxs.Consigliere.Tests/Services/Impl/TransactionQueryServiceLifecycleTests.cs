using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionQueryServiceLifecycleTests : RavenTestDriver
{
    [Fact]
    public async Task GetTransactionStateAsync_RebuildsProjectionAndReturnsLifecyclePayload()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var blockJournal = new RavenObservationJournal<BlockObservation>(store);
        var service = new TransactionQueryService(
            store,
            new TxLifecycleProjectionReader(store),
            new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store))
        );

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

        var state = await service.GetTransactionStateAsync("tx-1");

        Assert.True(state.Known);
        Assert.Equal("confirmed", state.LifecycleStatus);
        Assert.True(state.Authoritative);
        Assert.Equal(["node"], state.SeenBySources);
        Assert.Equal("block-1", state.BlockHash);
        Assert.Equal(100, state.BlockHeight);
        Assert.True(state.PayloadAvailable);
    }
}
