using System.Collections.Concurrent;
using System.Diagnostics;

using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services.Impl;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Benchmarks.Realtime;

public sealed class RealtimeFanoutBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    public async Task<RealtimeFanoutBenchmarkMetrics> MeasureSeenFanoutAsync(
        RealtimeFanoutBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var addresses = Enumerable.Range(0, scenario.AddressFanout).Select(VNextBenchmarkFixtureFactory.Address).ToArray();
        var tokenIds = Enumerable.Range(0, scenario.TokenFanout).Select(VNextBenchmarkFixtureFactory.TokenId).ToArray();
        await SeedTrackedScopeAsync(store, addresses, tokenIds, cancellationToken);

        var dispatcher = new CountingRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);

        var sw = Stopwatch.StartNew();
        for (var i = 0; i < scenario.TransactionCount; i++)
        {
            await notifier.PublishTransactionSeenAsync(
                VNextBenchmarkFixtureFactory.CreateFilteredMessage(VNextBenchmarkFixtureFactory.TransferTxId(i), addresses, tokenIds),
                cancellationToken);
        }
        sw.Stop();

        return new RealtimeFanoutBenchmarkMetrics(
            $"{scenario.Name}:seen",
            scenario.TransactionCount,
            dispatcher.PublishedEvents,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TransactionCount, sw.ElapsedMilliseconds));
    }

    public async Task<RealtimeFanoutBenchmarkMetrics> MeasureConfirmedFanoutAsync(
        RealtimeFanoutBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        var addresses = Enumerable.Range(0, scenario.AddressFanout).Select(VNextBenchmarkFixtureFactory.Address).ToArray();
        var tokenIds = Enumerable.Range(0, scenario.TokenFanout).Select(VNextBenchmarkFixtureFactory.TokenId).ToArray();
        var blockHash = VNextBenchmarkFixtureFactory.BlockHash(2048);
        await SeedTrackedScopeAsync(store, addresses, tokenIds, cancellationToken);
        await SeedConfirmedTransactionsAsync(store, scenario.TransactionCount, addresses, tokenIds, blockHash, cancellationToken);

        var dispatcher = new CountingRealtimeEventDispatcher();
        var notifier = BuildNotifier(store, dispatcher);

        var sw = Stopwatch.StartNew();
        await notifier.PublishBlockProcessedAsync(900_000, blockHash, cancellationToken);
        sw.Stop();

        return new RealtimeFanoutBenchmarkMetrics(
            $"{scenario.Name}:confirmed",
            scenario.TransactionCount,
            dispatcher.PublishedEvents,
            sw.ElapsedMilliseconds,
            ToThroughputPerSecond(scenario.TransactionCount, sw.ElapsedMilliseconds));
    }

    private static ManagedScopeRealtimeNotifier BuildNotifier(IDocumentStore store, CountingRealtimeEventDispatcher dispatcher)
    {
        var journalReader = new RavenObservationJournalReader(store);
        var txRebuilder = new TxLifecycleProjectionRebuilder(store, journalReader);
        return new ManagedScopeRealtimeNotifier(store, new TrackedEntityReadinessService(store), txRebuilder, dispatcher);
    }

    private static async Task SeedTrackedScopeAsync(IDocumentStore store, IReadOnlyCollection<string> addresses, IReadOnlyCollection<string> tokenIds, CancellationToken cancellationToken)
    {
        foreach (var address in addresses)
            await VNextBenchmarkFixtureFactory.SeedTrackedAddressStatusAsync(store, address, cancellationToken: cancellationToken);

        foreach (var tokenId in tokenIds)
            await VNextBenchmarkFixtureFactory.SeedTrackedTokenStatusAsync(store, tokenId, cancellationToken: cancellationToken);
    }

    private static async Task SeedConfirmedTransactionsAsync(
        IDocumentStore store,
        int transactionCount,
        IReadOnlyCollection<string> addresses,
        IReadOnlyCollection<string> tokenIds,
        string blockHash,
        CancellationToken cancellationToken)
    {
        var txJournal = new RavenObservationJournal<TxObservation>(store);
        for (var i = 0; i < transactionCount; i++)
        {
            var txId = VNextBenchmarkFixtureFactory.TransferTxId(i);
            await VNextBenchmarkFixtureFactory.SeedTransactionScopeAsync(store, txId, addresses, tokenIds, cancellationToken);
            await txJournal.AppendAsync(
                VNextBenchmarkFixtureFactory.CreateTxObservation(TxObservationEventType.SeenInBlock, txId, 5000 + i, blockHash, 900_000, transactionIndex: i),
                cancellationToken);
        }
    }

    private static double ToThroughputPerSecond(int operations, long elapsedMilliseconds)
        => elapsedMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsedMilliseconds;

    private sealed class CountingRealtimeEventDispatcher : IRealtimeEventDispatcher
    {
        private int _publishedEvents;
        private readonly ConcurrentDictionary<string, byte> _subscriptions = new(StringComparer.OrdinalIgnoreCase);

        public int PublishedEvents => _publishedEvents;

        public Task SubscribeToAddressStream(string connectionId, string address)
        {
            _subscriptions.TryAdd($"a:{connectionId}:{address}", 0);
            return Task.CompletedTask;
        }

        public Task UnsubscribeToAddressStream(string connectionId, string address)
        {
            _subscriptions.TryRemove($"a:{connectionId}:{address}", out _);
            return Task.CompletedTask;
        }

        public Task SubscribeToTokenStream(string connectionId, string tokenId)
        {
            _subscriptions.TryAdd($"t:{connectionId}:{tokenId}", 0);
            return Task.CompletedTask;
        }

        public Task UnsubscribeToTokenStream(string connectionId, string tokenId)
        {
            _subscriptions.TryRemove($"t:{connectionId}:{tokenId}", out _);
            return Task.CompletedTask;
        }

        public Task PublishToAddressAsync(string address, RealtimeEventResponse eventDto)
        {
            Interlocked.Increment(ref _publishedEvents);
            return Task.CompletedTask;
        }

        public Task PublishToTokenAsync(string tokenId, RealtimeEventResponse eventDto)
        {
            Interlocked.Increment(ref _publishedEvents);
            return Task.CompletedTask;
        }
    }
}
