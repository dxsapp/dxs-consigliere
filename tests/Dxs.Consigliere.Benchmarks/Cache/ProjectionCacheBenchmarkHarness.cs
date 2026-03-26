using System.Diagnostics;

using Dxs.Bsv;
using Dxs.Bsv.Script;
using Dxs.Common.Cache;
using Dxs.Consigliere.Benchmarks.Shared;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Data.Models.Addresses;
using Dxs.Consigliere.Data.Models.History;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Tokens;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Services;
using Dxs.Tests.Shared;

using Microsoft.Extensions.DependencyInjection;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Benchmarks.Cache;

public sealed class ProjectionCacheBenchmarkHarness : ConfiguredRavenBenchmarkTestDriver
{
    private const string TokenId = "1111111111111111111111111111111111111111";
    private const string PrimaryAddress = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";

    public async Task<ProjectionCacheBenchmarkMetrics> MeasureAsync(
        ProjectionCacheBenchmarkScenario scenario,
        CancellationToken cancellationToken = default)
    {
        using var store = GetDocumentStore();
        await SeedScenarioAsync(store, scenario, cancellationToken);

        using var cacheContext = CreateCacheContext(scenario.Backend);
        var cache = cacheContext.Cache;
        var invalidationSink = cacheContext.InvalidationSink;
        var keyFactory = cacheContext.KeyFactory;

        var addressReader = new AddressProjectionReader(store, cache, keyFactory);
        var historyReader = new AddressHistoryProjectionReader(store, new BenchmarkNetworkProvider(), cache, keyFactory);
        var tokenReader = new TokenProjectionReader(store, cache, keyFactory);

        var addresses = BuildAddresses(scenario.AddressCount);
        var historyRequest = new GetAddressHistoryRequest(PrimaryAddress, [TokenId], false, false, 0, scenario.Take);

        await historyReader.GetHistory(historyRequest, cancellationToken);
        await addressReader.LoadTokenBalancesAsync(addresses, [TokenId], cancellationToken);
        await addressReader.LoadTokenUtxosAsync(addresses, [TokenId], scenario.Take, cancellationToken);
        await tokenReader.LoadHistoryAsync(TokenId, scenario.Take, cancellationToken);

        var historyElapsed = await MeasureLoopAsync(
            scenario.QueryIterations,
            () => historyReader.GetHistory(historyRequest, cancellationToken));
        var balanceElapsed = await MeasureLoopAsync(
            scenario.QueryIterations,
            () => addressReader.LoadTokenBalancesAsync(addresses, [TokenId], cancellationToken));
        var utxoElapsed = await MeasureLoopAsync(
            scenario.QueryIterations,
            () => addressReader.LoadTokenUtxosAsync(addresses, [TokenId], scenario.Take, cancellationToken));
        var tokenHistoryElapsed = await MeasureLoopAsync(
            scenario.QueryIterations,
            () => tokenReader.LoadHistoryAsync(TokenId, scenario.Take, cancellationToken));

        var addressTags = keyFactory.GetAddressInvalidationTags([PrimaryAddress]);
        var tokenTags = keyFactory.GetTokenInvalidationTags([TokenId]);
        var invalidationElapsed = await MeasureLoopAsync(
            scenario.QueryIterations,
            async () =>
            {
                await invalidationSink.InvalidateTagsAsync(addressTags, cancellationToken);
                await historyReader.GetHistory(historyRequest, cancellationToken);
                await invalidationSink.InvalidateTagsAsync(tokenTags, cancellationToken);
                await tokenReader.LoadHistoryAsync(TokenId, scenario.Take, cancellationToken);
            });

        return new ProjectionCacheBenchmarkMetrics(
            scenario.Name,
            scenario.Backend,
            scenario.QueryIterations,
            (long)Math.Round(historyElapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
            ToThroughputPerSecond(scenario.QueryIterations, historyElapsed),
            (long)Math.Round(balanceElapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
            ToThroughputPerSecond(scenario.QueryIterations, balanceElapsed),
            (long)Math.Round(utxoElapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
            ToThroughputPerSecond(scenario.QueryIterations, utxoElapsed),
            (long)Math.Round(tokenHistoryElapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
            ToThroughputPerSecond(scenario.QueryIterations, tokenHistoryElapsed),
            (long)Math.Round(invalidationElapsed.TotalMilliseconds, MidpointRounding.AwayFromZero),
            ToThroughputPerSecond(scenario.QueryIterations, invalidationElapsed),
            cache.Count
        );
    }

    private static CacheContext CreateCacheContext(string backend)
    {
        if (string.Equals(backend, "azos", StringComparison.OrdinalIgnoreCase))
        {
            var cache = new AzosProjectionReadCacheSpike(
                10_000,
                TimeSpan.FromMinutes(5),
                $"projection-cache-{Guid.NewGuid():N}");
            return new CacheContext(cache, cache, new ProjectionReadCacheKeyFactory());
        }

        var services = new ServiceCollection();
        services.AddProjectionReadCache(options =>
        {
            options.MaxEntries = 10_000;
            options.DefaultSafetyTtl = TimeSpan.FromMinutes(5);
        });
        services.AddSingleton<IProjectionReadCacheKeyFactory, ProjectionReadCacheKeyFactory>();
        var provider = services.BuildServiceProvider();
        return new CacheContext(
            provider.GetRequiredService<IProjectionReadCache>(),
            provider.GetRequiredService<IProjectionCacheInvalidationSink>(),
            provider.GetRequiredService<IProjectionReadCacheKeyFactory>(),
            provider);
    }

    private static string[] BuildAddresses(int count)
        => Enumerable.Range(0, count)
            .Select(i => i == 0 ? PrimaryAddress : $"1Cache{i:D28}")
            .ToArray();

    private static async Task<TimeSpan> MeasureLoopAsync(int iterations, Func<Task> action)
    {
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < iterations; i++)
            await action();
        sw.Stop();
        return sw.Elapsed;
    }

    private static double ToThroughputPerSecond(int operations, TimeSpan elapsed)
        => elapsed.TotalMilliseconds <= 0
            ? operations
            : operations * 1000.0 / elapsed.TotalMilliseconds;

    private static async Task SeedScenarioAsync(
        IDocumentStore store,
        ProjectionCacheBenchmarkScenario scenario,
        CancellationToken cancellationToken)
    {
        using var session = store.OpenAsyncSession();
        var addresses = BuildAddresses(scenario.AddressCount);

        for (var i = 0; i < scenario.HistoryTransactionCount; i++)
        {
            var txId = $"cache-hist-{i:D4}";
            var timestamp = 1_710_900_000L + i;
            var metaOutput = new MetaOutput
            {
                Id = MetaOutput.GetId(txId, 0),
                TxId = txId,
                Vout = 0,
                Address = PrimaryAddress,
                TokenId = TokenId,
                Satoshis = 1 + i,
                Type = ScriptType.P2STAS,
                ScriptPubKey = $"script-{txId}",
                Spent = false
            };

            await session.StoreAsync(metaOutput, metaOutput.Id, cancellationToken);
            await session.StoreAsync(
                new MetaTransaction
                {
                    Id = txId,
                    Inputs = [],
                    Outputs = [new MetaTransaction.Output(metaOutput)],
                    Addresses = [PrimaryAddress],
                    TokenIds = [TokenId],
                    IsStas = true,
                    Timestamp = timestamp,
                    Height = i + 1,
                    IllegalRoots = [],
                    MissingTransactions = []
                },
                txId,
                cancellationToken);
            await session.StoreAsync(
                new AddressProjectionAppliedTransactionDocument
                {
                    Id = AddressProjectionAppliedTransactionDocument.GetId(txId),
                    TxId = txId,
                    AppliedState = AddressProjectionApplicationState.Confirmed,
                    ConfirmedBlockHash = $"block-{i:D4}",
                    LastSequence = i + 1,
                    Credits =
                    [
                        new AddressProjectionUtxoSnapshot
                        {
                            Id = AddressUtxoProjectionDocument.GetId(txId, 0),
                            TxId = txId,
                            Vout = 0,
                            Address = PrimaryAddress,
                            TokenId = TokenId,
                            Satoshis = 1 + i,
                            ScriptType = ScriptType.P2STAS,
                            ScriptPubKey = $"script-{txId}"
                        }
                    ],
                    Debits = []
                },
                AddressProjectionAppliedTransactionDocument.GetId(txId),
                cancellationToken);
            await session.StoreAsync(
                new TokenHistoryProjectionDocument
                {
                    Id = TokenHistoryProjectionDocument.GetId(TokenId, txId),
                    TokenId = TokenId,
                    TxId = txId,
                    Timestamp = timestamp,
                    Height = i + 1,
                    ReceivedSatoshis = 1 + i,
                    SpentSatoshis = 0,
                    BalanceDeltaSatoshis = 1 + i,
                    ValidationStatus = TokenProjectionValidationStatus.Valid,
                    ProtocolType = TokenProjectionProtocolType.Stas,
                    LastSequence = i + 1
                },
                TokenHistoryProjectionDocument.GetId(TokenId, txId),
                cancellationToken);
        }

        var totalSupply = 0L;
        for (var i = 0; i < addresses.Length; i++)
        {
            var address = addresses[i];
            var balance = 100 + i;
            totalSupply += balance;

            await session.StoreAsync(
                new AddressBalanceProjectionDocument
                {
                    Id = AddressBalanceProjectionDocument.GetId(address, TokenId),
                    Address = address,
                    TokenId = TokenId,
                    Satoshis = balance,
                    LastSequence = scenario.HistoryTransactionCount
                },
                AddressBalanceProjectionDocument.GetId(address, TokenId),
                cancellationToken);

            for (var utxoIndex = 0; utxoIndex < scenario.UtxoCountPerAddress; utxoIndex++)
            {
                var txId = $"cache-utxo-{i:D3}-{utxoIndex:D3}";
                await session.StoreAsync(
                    new AddressUtxoProjectionDocument
                    {
                        Id = AddressUtxoProjectionDocument.GetId(txId, 0),
                        TxId = txId,
                        Vout = 0,
                        Address = address,
                        TokenId = TokenId,
                        Satoshis = 1 + utxoIndex,
                        ScriptPubKey = $"script-{txId}",
                        ScriptType = ScriptType.P2STAS,
                        LastSequence = scenario.HistoryTransactionCount
                    },
                    AddressUtxoProjectionDocument.GetId(txId, 0),
                    cancellationToken);
            }
        }

        await session.StoreAsync(
            new TokenStateProjectionDocument
            {
                Id = TokenStateProjectionDocument.GetId(TokenId),
                TokenId = TokenId,
                ProtocolType = TokenProjectionProtocolType.Stas,
                ValidationStatus = TokenProjectionValidationStatus.Valid,
                IssuanceKnown = true,
                Issuer = PrimaryAddress,
                RedeemAddress = PrimaryAddress,
                TotalKnownSupply = totalSupply,
                LastSequence = scenario.HistoryTransactionCount
            },
            TokenStateProjectionDocument.GetId(TokenId),
            cancellationToken);

        await session.SaveChangesAsync(cancellationToken);
    }

    private sealed class BenchmarkNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }

    private sealed record CacheContext(
        IProjectionReadCache Cache,
        IProjectionCacheInvalidationSink InvalidationSink,
        IProjectionReadCacheKeyFactory KeyFactory,
        IDisposable? OwnedProvider = null) : IDisposable
    {
        public void Dispose()
        {
            if (OwnedProvider is not null)
            {
                OwnedProvider.Dispose();
                return;
            }

            if (Cache is IDisposable disposableCache)
                disposableCache.Dispose();
        }
    }
}
