using Dxs.Bsv.BitcoinMonitor;
using Dxs.Bsv.BitcoinMonitor.Models;
using Dxs.Bsv;
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

public class StasDependencyRevalidationCascadeIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task HandleTransactionChangedAsync_CascadesAcrossDirectEdges()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var dependencyStore = new TokenValidationDependencyStore(store);
        var replayingStore = new ReplayingMetaTransactionStore();
        var coordinator = new StasDependencyRevalidationCoordinator(store, replayingStore, NullLogger.Instance);
        replayingStore.AttachCoordinator(coordinator);

        await SeedMetaTransactionAsync(store, "root");
        await SeedMetaTransactionAsync(store, "child");
        await SeedMetaTransactionAsync(store, "grandchild");

        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("child", ["root"], []));
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("grandchild", ["child"], []));

        await coordinator.HandleTransactionChangedAsync("root");

        Assert.Equal(["child", "grandchild"], replayingStore.UpdatedTxIds);
    }

    [Fact]
    public async Task HandleTransactionDeletedAsync_CascadesAcrossDirectEdges()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var dependencyStore = new TokenValidationDependencyStore(store);
        var replayingStore = new ReplayingMetaTransactionStore();
        var coordinator = new StasDependencyRevalidationCoordinator(store, replayingStore, NullLogger.Instance);
        replayingStore.AttachCoordinator(coordinator);

        await SeedMetaTransactionAsync(store, "child");
        await SeedMetaTransactionAsync(store, "grandchild");

        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("child", ["root"], ["root"]));
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("grandchild", ["child"], []));

        await coordinator.HandleTransactionDeletedAsync("root");

        Assert.Equal(["child", "grandchild"], replayingStore.UpdatedTxIds);
        Assert.Empty(await dependencyStore.LoadDirectDependentsAsync("root"));
    }

    private static async Task SeedMetaTransactionAsync(IDocumentStore store, string txId)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(new MetaTransaction
        {
            Id = txId,
            Inputs = [],
            Outputs = []
        }, txId);
        await session.SaveChangesAsync();
    }

    private sealed class ReplayingMetaTransactionStore : IMetaTransactionStore
    {
        private readonly List<string> _updatedTxIds = [];
        private StasDependencyRevalidationCoordinator? _coordinator;

        public IReadOnlyList<string> UpdatedTxIds => _updatedTxIds;

        public void AttachCoordinator(StasDependencyRevalidationCoordinator coordinator)
            => _coordinator = coordinator;

        public async Task UpdateStasAttributes(string txId)
        {
            _updatedTxIds.Add(txId);

            if (_coordinator is not null)
                await _coordinator.HandleTransactionChangedAsync(txId);
        }

        public Task<List<Address>> GetWatchingAddresses() => Task.FromResult(new List<Address>());

        public Task<List<TokenId>> GetWatchingTokens() => Task.FromResult(new List<TokenId>());

        public Task<TransactionProcessStatus> SaveTransaction(
            Transaction transaction,
            long timestamp,
            string firstOutToRedeem,
            string? blockHash = null,
            int? blockHeight = null,
            int? indexInBlock = null
        ) => Task.FromResult(TransactionProcessStatus.NotModified);

        public Task<Transaction?> TryRemoveTransaction(string id) => Task.FromResult<Transaction?>(null);
    }
}
