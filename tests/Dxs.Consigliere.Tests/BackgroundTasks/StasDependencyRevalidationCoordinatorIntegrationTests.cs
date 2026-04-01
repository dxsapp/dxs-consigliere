using Dxs.Consigliere.BackgroundTasks;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Tests.Shared;

using Microsoft.Extensions.Logging.Abstractions;

using Moq;

using Raven.Client.Documents;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.BackgroundTasks;

public class StasDependencyRevalidationCoordinatorIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task HandleTransactionChangedAsync_RevalidatesDirectDependentsOnly()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var dependencyStore = new TokenValidationDependencyStore(store);
        var txStore = new Mock<IMetaTransactionStore>();
        var sut = new StasDependencyRevalidationCoordinator(dependencyStore, store, txStore.Object, NullLogger.Instance);

        await SeedMetaTransactionAsync(store, new MetaTransaction
        {
            Id = "parent-tx",
            Inputs = [],
            Outputs = []
        });
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("child-a", ["parent-tx"], []));
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("child-b", ["parent-tx"], ["parent-tx"]));
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("unrelated", ["other-parent"], []));

        await sut.HandleTransactionChangedAsync("parent-tx");

        txStore.Verify(x => x.UpdateStasAttributes("child-a"), Times.Once);
        txStore.Verify(x => x.UpdateStasAttributes("child-b"), Times.Once);
        txStore.Verify(x => x.UpdateStasAttributes("unrelated"), Times.Never);
    }

    [Fact]
    public async Task HandleTransactionDeletedAsync_RemovesDependencyFactsAndRevalidatesDependents()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var dependencyStore = new TokenValidationDependencyStore(store);
        var txStore = new Mock<IMetaTransactionStore>();
        var sut = new StasDependencyRevalidationCoordinator(dependencyStore, store, txStore.Object, NullLogger.Instance);

        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("parent-tx", [], []));
        await dependencyStore.UpsertAsync(TokenValidationDependencySnapshot.Create("child-a", ["parent-tx"], []));

        await sut.HandleTransactionDeletedAsync("parent-tx");

        txStore.Verify(x => x.UpdateStasAttributes("child-a"), Times.Once);
        Assert.Null(await dependencyStore.LoadAsync("parent-tx"));
        Assert.Empty(await dependencyStore.LoadDirectDependentsAsync("parent-tx"));
    }

    private static async Task SeedMetaTransactionAsync(IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);
        await session.SaveChangesAsync();
    }
}
