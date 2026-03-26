using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Transactions;

public class TokenValidationDependencyStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task UpsertAsync_StoresDirectDependenciesAndReverseDependents()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new TokenValidationDependencyStore(store);

        await sut.UpsertAsync(
            TokenValidationDependencySnapshot.Create(
                "tx-child",
                ["tx-parent-a", "tx-parent-b"],
                ["tx-parent-b"]
            )
        );

        var dependency = await sut.LoadAsync("tx-child");
        var parentADependents = await sut.LoadDirectDependentsAsync("tx-parent-a");
        var parentBDependents = await sut.LoadDirectDependentsAsync("tx-parent-b");

        Assert.NotNull(dependency);
        Assert.Equal(["tx-parent-a", "tx-parent-b"], dependency!.DependsOnTxIds);
        Assert.Equal(["tx-parent-b"], dependency.MissingDependencies);
        Assert.Equal(["tx-child"], parentADependents);
        Assert.Equal(["tx-child"], parentBDependents);
    }

    [Fact]
    public async Task UpsertAsync_RewritesReverseDependentsWhenDirectEdgesChange()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new TokenValidationDependencyStore(store);

        await sut.UpsertAsync(
            TokenValidationDependencySnapshot.Create(
                "tx-child",
                ["tx-parent-a", "tx-parent-b"],
                ["tx-parent-b"]
            )
        );
        await sut.UpsertAsync(
            TokenValidationDependencySnapshot.Create(
                "tx-child",
                ["tx-parent-c"],
                []
            )
        );

        var dependency = await sut.LoadAsync("tx-child");
        var parentADependents = await sut.LoadDirectDependentsAsync("tx-parent-a");
        var parentBDependents = await sut.LoadDirectDependentsAsync("tx-parent-b");
        var parentCDependents = await sut.LoadDirectDependentsAsync("tx-parent-c");

        Assert.NotNull(dependency);
        Assert.Equal(["tx-parent-c"], dependency!.DependsOnTxIds);
        Assert.Empty(dependency.MissingDependencies);
        Assert.Empty(parentADependents);
        Assert.Empty(parentBDependents);
        Assert.Equal(["tx-child"], parentCDependents);
    }

    [Fact]
    public async Task UpsertAsync_FromMetaTransaction_NormalizesMissingDependenciesToDirectEdges()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new TokenValidationDependencyStore(store);
        var metaTransaction = new MetaTransaction
        {
            Id = "tx-token",
            Inputs =
            [
                new MetaTransaction.Input
                {
                    TxId = "tx-parent-a",
                    Vout = 0
                },
                new MetaTransaction.Input
                {
                    TxId = "tx-parent-b",
                    Vout = 1
                }
            ],
            MissingTransactions = ["tx-parent-b", "tx-parent-b", "not-an-input"]
        };

        await sut.UpsertAsync(metaTransaction);

        var dependency = await sut.LoadAsync("tx-token");

        Assert.NotNull(dependency);
        Assert.Equal(["tx-parent-a", "tx-parent-b"], dependency!.DependsOnTxIds);
        Assert.Equal(["tx-parent-b"], dependency.MissingDependencies);
    }

    [Fact]
    public async Task RemoveAsync_CleansReverseDependents()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new TokenValidationDependencyStore(store);

        await sut.UpsertAsync(
            TokenValidationDependencySnapshot.Create(
                "tx-child",
                ["tx-parent-a"],
                ["tx-parent-a"]
            )
        );

        await sut.RemoveAsync("tx-child");

        var dependency = await sut.LoadAsync("tx-child");
        var parentADependents = await sut.LoadDirectDependentsAsync("tx-parent-a");

        Assert.Null(dependency);
        Assert.Empty(parentADependents);
    }
}
