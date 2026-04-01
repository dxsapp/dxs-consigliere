using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv.Models;
using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Tests.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.BackgroundTasks;

public class ValidationDependencyRepairProcessorTests : RavenTestDriver
{
    [Fact]
    public async Task ProcessDueAsync_ResolvesWorkAndTriggersRevalidationForFetchedDependencies()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = "tx-main",
            IsStas = true,
            MissingTransactions = ["dep-a"],
            Inputs = [],
            Outputs = []
        });

        var workStore = new ValidationRepairWorkItemStore(store);
        await workStore.ScheduleAsync("tx-main", ValidationRepairReasons.PublicValidate, ["dep-a"]);

        var metaStore = new FakeMetaTransactionStore(store, "tx-main", clearMissingOnUpdate: true);
        var coordinator = new FakeCoordinator();
        var dependencyService = new FakeValidationDependencyService(["dep-a"], [], null);
        var sut = new ValidationDependencyRepairProcessor(store, workStore, dependencyService, metaStore, coordinator, NullLogger<ValidationDependencyRepairProcessor>.Instance);

        var processed = await sut.ProcessDueAsync();
        var saved = await workStore.LoadAsync("tx-main");

        Assert.Equal(1, processed);
        Assert.NotNull(saved);
        Assert.Equal(ValidationRepairStates.Resolved, saved!.State);
        Assert.Equal(["tx-main"], metaStore.UpdatedTxIds);
        Assert.Equal(["dep-a"], coordinator.ChangedTxIds);
    }

    [Fact]
    public async Task ProcessDueAsync_KeepsPendingWorkWhenDependenciesRemainMissing()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = "tx-pending",
            IsStas = true,
            MissingTransactions = ["dep-a", "dep-b"],
            Inputs = [],
            Outputs = []
        });

        var workStore = new ValidationRepairWorkItemStore(store);
        await workStore.ScheduleAsync("tx-pending", ValidationRepairReasons.PublicValidate, ["dep-a", "dep-b"]);

        var metaStore = new FakeMetaTransactionStore(store, "tx-pending", clearMissingOnUpdate: false);
        var coordinator = new FakeCoordinator();
        var dependencyService = new FakeValidationDependencyService(["dep-a"], ["dep-b"], "partial_dependency_resolution");
        var sut = new ValidationDependencyRepairProcessor(store, workStore, dependencyService, metaStore, coordinator, NullLogger<ValidationDependencyRepairProcessor>.Instance);

        await sut.ProcessDueAsync();
        var saved = await workStore.LoadAsync("tx-pending");

        Assert.NotNull(saved);
        Assert.Equal(ValidationRepairStates.Pending, saved!.State);
        Assert.Equal(["dep-a", "dep-b"], saved.MissingDependencies);
        Assert.Equal("partial_dependency_resolution", saved.LastError);
        Assert.NotNull(saved.NextAttemptAt);
        Assert.Equal(["dep-a"], coordinator.ChangedTxIds);
    }

    [Fact]
    public async Task ProcessDueAsync_BlocksWhenTargetTransactionIsMissing()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var workStore = new ValidationRepairWorkItemStore(store);
        await workStore.ScheduleAsync("tx-missing", ValidationRepairReasons.PublicValidate, ["dep-a"]);

        var metaStore = new FakeMetaTransactionStore(store, "tx-missing", clearMissingOnUpdate: false);
        var coordinator = new FakeCoordinator();
        var dependencyService = new FakeValidationDependencyService([], ["dep-a"], "not_used");
        var sut = new ValidationDependencyRepairProcessor(store, workStore, dependencyService, metaStore, coordinator, NullLogger<ValidationDependencyRepairProcessor>.Instance);

        await sut.ProcessDueAsync();
        var saved = await workStore.LoadAsync("tx-missing");

        Assert.NotNull(saved);
        Assert.Equal(ValidationRepairStates.Blocked, saved!.State);
        Assert.Equal("target_transaction_missing", saved.LastError);
    }

    private static async Task SeedAsync(IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);
        await session.SaveChangesAsync();
    }

    private sealed class FakeValidationDependencyService(
        IReadOnlyList<string> fetchedDependencies,
        IReadOnlyList<string> remainingDependencies,
        string? lastError) : IValidationDependencyService
    {
        public Task<ValidationDependencyResolutionResult> ResolveAsync(string entityId, IReadOnlyList<string> missingDependencies, CancellationToken cancellationToken = default)
            => Task.FromResult(new ValidationDependencyResolutionResult(fetchedDependencies, remainingDependencies, lastError));
    }

    private sealed class FakeCoordinator : IStasDependencyRevalidationCoordinator
    {
        public List<string> ChangedTxIds { get; } = [];

        public Task HandleTransactionChangedAsync(string txId, CancellationToken cancellationToken = default)
        {
            ChangedTxIds.Add(txId);
            return Task.CompletedTask;
        }

        public Task HandleTransactionDeletedAsync(string txId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }

    private sealed class FakeMetaTransactionStore(IDocumentStore store, string targetId, bool clearMissingOnUpdate) : IMetaTransactionStore
    {
        public List<string> UpdatedTxIds { get; } = [];

        public async Task UpdateStasAttributes(string txId)
        {
            UpdatedTxIds.Add(txId);
            if (!clearMissingOnUpdate || !string.Equals(txId, targetId, StringComparison.Ordinal))
                return;

            using var session = store.OpenAsyncSession();
            var document = await session.LoadAsync<MetaTransaction>(txId);
            if (document is null)
                return;

            document.MissingTransactions = [];
            await session.SaveChangesAsync();
        }

        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(new List<Address>());
        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(new List<TokenId>());
        public Task<TransactionProcessStatus> SaveTransaction(Transaction transaction, long timestamp, string firstOutToRedeem, string blockHash = null, int? blockHeight = null, int? indexInBlock = null)
            => Task.FromResult(TransactionProcessStatus.NotModified);
        public Task<Transaction?> TryRemoveTransaction(string id) => Task.FromResult<Transaction?>(null);
    }
}
