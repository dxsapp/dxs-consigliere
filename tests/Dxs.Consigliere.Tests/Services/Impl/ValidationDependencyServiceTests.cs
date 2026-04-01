using System.Net;

using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Bsv.Transactions.Build;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class ValidationDependencyServiceTests : RavenTestDriver
{
    [Fact]
    public async Task ResolveAsync_WalksLineageBackwardsUntilValidIssue()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();

        var root = CreateRawTransaction("root");
        var parent = CreateRawTransaction("parent");

        var savedTransactions = new Dictionary<string, MetaTransaction>(StringComparer.Ordinal)
        {
            [parent.TxId] = new MetaTransaction
            {
                Id = parent.TxId,
                IsStas = true,
                MissingTransactions = [root.TxId],
                Inputs = [new MetaTransaction.Input { TxId = root.TxId, Vout = 0 }],
                Outputs = []
            },
            [root.TxId] = new MetaTransaction
            {
                Id = root.TxId,
                IsStas = true,
                IsIssue = true,
                IsValidIssue = true,
                AllStasInputsKnown = true,
                MissingTransactions = [],
                Inputs = [],
                Outputs = []
            }
        };

        var acquisition = new FakeAcquisitionService(new Dictionary<string, byte[]>
        {
            [parent.TxId] = parent.Raw,
            [root.TxId] = root.Raw
        });
        var metaStore = new FakeMetaTransactionStore(store, savedTransactions);
        var sut = new ValidationDependencyService(
            store,
            acquisition,
            metaStore,
            new NetworkProvider(Options.Create(new Dxs.Consigliere.Configs.NetworkConfig { Network = "mainnet" })),
            NullLogger<ValidationDependencyService>.Instance);

        var result = await sut.ResolveAsync("child", [parent.TxId]);

        Assert.Equal([parent.TxId, root.TxId], result.FetchedDependencies);
        Assert.Empty(result.RemainingDependencies);
        Assert.Equal(ValidationRepairStopReasons.ValidIssueReached, result.StopReason);
        Assert.Equal(2, result.FetchCount);
        Assert.Equal(2, result.VisitedCount);
        Assert.Equal(2, result.MaxTraversalDepth);
        Assert.Equal([ExternalChainCapability.ValidationFetch, ExternalChainCapability.ValidationFetch], acquisition.CapabilitiesSeen);
    }

    [Fact]
    public async Task ResolveAsync_StopsWithMissingDependencyWhenParentCannotBeFetched()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new ValidationDependencyService(
            store,
            new FakeAcquisitionService(new Dictionary<string, byte[]>()),
            new FakeMetaTransactionStore(store, new Dictionary<string, MetaTransaction>(StringComparer.Ordinal)),
            new NetworkProvider(Options.Create(new Dxs.Consigliere.Configs.NetworkConfig { Network = "mainnet" })),
            NullLogger<ValidationDependencyService>.Instance);

        var result = await sut.ResolveAsync("child", ["missing-parent"]);

        Assert.Empty(result.FetchedDependencies);
        Assert.Equal(["missing-parent"], result.RemainingDependencies);
        Assert.Equal(ValidationRepairStopReasons.MissingDependency, result.StopReason);
        Assert.Equal(0, result.FetchCount);
        Assert.Equal(1, result.VisitedCount);
        Assert.Equal(1, result.MaxTraversalDepth);
    }

    [Fact]
    public async Task ResolveAsync_StopsWithBudgetExceededWhenTraversalDepthRunsTooDeep()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();

        const int chainLength = 130;
        var acquisitionMap = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var savedTransactions = new Dictionary<string, MetaTransaction>(StringComparer.Ordinal);
        string? firstTxId = null;
        string? previousTxId = null;

        for (var i = 1; i <= chainLength; i++)
        {
            var rawTx = CreateRawTransaction($"depth-{i}");
            acquisitionMap[rawTx.TxId] = rawTx.Raw;

            var dependency = previousTxId is null ? [] : new[] { previousTxId };
            savedTransactions[rawTx.TxId] = new MetaTransaction
            {
                Id = rawTx.TxId,
                IsStas = true,
                MissingTransactions = dependency.ToList(),
                Inputs = previousTxId is null ? [] : [new MetaTransaction.Input { TxId = previousTxId, Vout = 0 }],
                Outputs = []
            };

            previousTxId = rawTx.TxId;
            firstTxId = rawTx.TxId;
        }

        var sut = new ValidationDependencyService(
            store,
            new FakeAcquisitionService(acquisitionMap),
            new FakeMetaTransactionStore(store, savedTransactions),
            new NetworkProvider(Options.Create(new Dxs.Consigliere.Configs.NetworkConfig { Network = "mainnet" })),
            NullLogger<ValidationDependencyService>.Instance);

        var result = await sut.ResolveAsync("child", [firstTxId!]);

        Assert.Equal(ValidationRepairStopReasons.BudgetExceeded, result.StopReason);
        Assert.NotEmpty(result.FetchedDependencies);
        Assert.NotEmpty(result.RemainingDependencies);
        Assert.True(result.FetchCount >= 128);
        Assert.True(result.MaxTraversalDepth >= 128);
    }

    [Fact]
    public async Task ResolveAsync_ClassifiesProvider429AsRateLimited()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var acquisition = new FakeAcquisitionService(
            new Dictionary<string, byte[]>(),
            errorTxId: "limited-parent",
            errorFactory: () => new HttpRequestException("rate limited", null, HttpStatusCode.TooManyRequests));
        var sut = new ValidationDependencyService(
            store,
            acquisition,
            new FakeMetaTransactionStore(store, new Dictionary<string, MetaTransaction>(StringComparer.Ordinal)),
            new NetworkProvider(Options.Create(new Dxs.Consigliere.Configs.NetworkConfig { Network = "mainnet" })),
            NullLogger<ValidationDependencyService>.Instance);

        var result = await sut.ResolveAsync("child", ["limited-parent"]);

        Assert.Equal(ValidationRepairStopReasons.ProviderRateLimited, result.StopReason);
        Assert.Equal(["limited-parent"], result.RemainingDependencies);
        Assert.Equal(1, result.VisitedCount);
    }

    private static RawTxFixture CreateRawTransaction(string label)
    {
        var builder = TransactionBuilder.Init()
            .AddNullDataOutput([(byte)label.Length]);

        var raw = builder.ToBytes();
        return new RawTxFixture(builder.Id, raw);
    }

    private sealed record RawTxFixture(string TxId, byte[] Raw);

    private sealed class FakeAcquisitionService(
        IReadOnlyDictionary<string, byte[]> payloads,
        string? errorTxId = null,
        Func<Exception>? errorFactory = null) : IUpstreamTransactionAcquisitionService
    {
        public List<string> CapabilitiesSeen { get; } = [];

        public Task<RawTransactionFetchResult?> TryGetAsync(string txId, string capability, CancellationToken cancellationToken = default)
        {
            CapabilitiesSeen.Add(capability);

            if (string.Equals(txId, errorTxId, StringComparison.Ordinal))
                throw errorFactory!();

            return Task.FromResult(
                payloads.TryGetValue(txId, out var raw)
                    ? new RawTransactionFetchResult(ExternalChainProviderName.JungleBus, raw)
                    : null);
        }
    }

    private sealed class FakeMetaTransactionStore(
        IDocumentStore store,
        IReadOnlyDictionary<string, MetaTransaction> savedTransactions) : IMetaTransactionStore
    {
        public async Task UpdateStasAttributes(string txId)
        {
            await Task.CompletedTask;
        }

        public async Task<TransactionProcessStatus> SaveTransaction(Transaction transaction, long timestamp, string firstOutToRedeem, string blockHash = null, int? blockHeight = null, int? indexInBlock = null)
        {
            if (!savedTransactions.TryGetValue(transaction.Id, out var meta))
                throw new InvalidOperationException($"No fake meta transaction registered for `{transaction.Id}`.");

            using var session = store.OpenAsyncSession();
            await session.StoreAsync(meta, meta.Id);
            await session.SaveChangesAsync();
            return TransactionProcessStatus.FoundInMempool;
        }

        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(new List<Address>());
        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(new List<TokenId>());
        public Task<Transaction?> TryRemoveTransaction(string id) => Task.FromResult<Transaction?>(null);
    }
}
