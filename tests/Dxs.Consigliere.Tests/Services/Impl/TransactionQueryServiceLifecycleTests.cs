using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Common.Cache;
using Dxs.Common.Journal;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Microsoft.Extensions.DependencyInjection;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionQueryServiceLifecycleTests : RavenTestDriver
{
    private const string TxId = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

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
                        TxId,
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_000)
                    ),
                    new RawTransactionPayloadReference($"raw-tx-payloads/{TxId}", TxId, RawTransactionPayloadCompressionAlgorithm.None)
                ),
                new DedupeFingerprint($"node|tx_seen_in_mempool|{TxId}")
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
                        TxId,
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_100),
                        "block-1",
                        100,
                        3
                    )
                ),
                new DedupeFingerprint($"node|tx_seen_in_block|{TxId}|block-1")
            )
        );

        var state = await service.GetTransactionStateAsync(TxId);

        Assert.True(state.Known);
        Assert.Equal("confirmed", state.LifecycleStatus);
        Assert.True(state.Authoritative);
        Assert.Equal(["node"], state.SeenBySources);
        Assert.Equal("block-1", state.BlockHash);
        Assert.Equal(100, state.BlockHeight);
        Assert.True(state.PayloadAvailable);
    }

    [Fact]
    public async Task GetTransactionStateAsync_InvalidatesCachedLifecycleAfterConfirmation()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        var services = CreateCacheServices();
        var service = new TransactionQueryService(
            store,
            new TxLifecycleProjectionReader(
                store,
                services.GetRequiredService<IProjectionReadCache>(),
                services.GetRequiredService<IProjectionReadCacheKeyFactory>()),
            new TxLifecycleProjectionRebuilder(
                store,
                new RavenObservationJournalReader(store),
                services.GetRequiredService<IProjectionCacheInvalidationSink>(),
                services.GetRequiredService<IProjectionReadCacheKeyFactory>())
        );

        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInMempool,
                        TxObservationSource.Node,
                        TxId,
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_000)
                    )
                ),
                new DedupeFingerprint($"node|tx_seen_in_mempool|{TxId}")
            )
        );

        var first = await service.GetTransactionStateAsync(TxId);
        Assert.Equal("seen_in_mempool", first.LifecycleStatus);

        await txJournal.AppendAsync(
            new ObservationJournalAppendRequest<ObservationJournalEntry<TxObservation>>(
                new ObservationJournalEntry<TxObservation>(
                    new TxObservation(
                        TxObservationEventType.SeenInBlock,
                        TxObservationSource.Node,
                        TxId,
                        DateTimeOffset.FromUnixTimeSeconds(1_710_000_100),
                        "block-1",
                        100,
                        2
                    )
                ),
                new DedupeFingerprint($"node|tx_seen_in_block|{TxId}|block-1")
            )
        );

        var second = await service.GetTransactionStateAsync(TxId);
        Assert.Equal("confirmed", second.LifecycleStatus);
        Assert.Equal("block-1", second.BlockHash);
        Assert.Equal(100, second.BlockHeight);
    }

    private static ServiceProvider CreateCacheServices()
    {
        var services = new ServiceCollection();
        services.AddProjectionReadCache(options =>
        {
            options.MaxEntries = 128;
            options.DefaultSafetyTtl = TimeSpan.FromMinutes(5);
        });
        services.AddSingleton<IProjectionReadCacheKeyFactory, ProjectionReadCacheKeyFactory>();
        return services.BuildServiceProvider();
    }
}
