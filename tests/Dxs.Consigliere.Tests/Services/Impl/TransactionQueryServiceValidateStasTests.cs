using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;
using Dxs.Tests.Shared;

using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Services.Impl;

public class TransactionQueryServiceValidateStasTests : RavenTestDriver
{
    [Fact]
    public async Task RejectsNonStasTransactionEvenWhenInputsAreIncomplete()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = new string('a', 64),
            IsStas = false,
            AllStasInputsKnown = false,
            MissingTransactions = ["missing-parent"],
            TokenIds = [],
            IllegalRoots = []
        });

        var service = CreateService(store);

        var exception = await Assert.ThrowsAsync<TransactionQueryException>(() =>
            service.ValidateStasTransactionAsync(new string('a', 64)));

        Assert.Equal(TransactionQueryErrorKind.NotStas, exception.Kind);
    }

    [Fact]
    public async Task ReturnsUnknownVerdictWhenLineageDependenciesAreMissing()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = new string('b', 64),
            IsStas = true,
            IsIssue = false,
            AllStasInputsKnown = false,
            MissingTransactions = ["missing-parent-1", "missing-parent-2"],
            TokenIds = ["token-1"],
            IllegalRoots = [],
            StasValidationStatus = TokenProjectionValidationStatus.Unknown
        });

        var service = CreateService(store);

        var result = await service.ValidateStasTransactionAsync(new string('b', 64));

        Assert.True(result.AskLater);
        Assert.False(result.IsLegal);
        Assert.Equal(TokenProjectionValidationStatus.Unknown, result.ValidationStatus);
        Assert.False(result.B2GResolved);
        Assert.Equal(["missing-parent-1", "missing-parent-2"], result.MissingDependencies);
    }

    [Fact]
    public async Task ReturnsInvalidVerdictWhenIllegalRootsArePresent()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = new string('c', 64),
            IsStas = true,
            IsIssue = false,
            AllStasInputsKnown = true,
            MissingTransactions = [],
            TokenIds = ["token-2"],
            IllegalRoots = ["illegal-root-1"],
            StasValidationStatus = TokenProjectionValidationStatus.Invalid
        });

        var service = CreateService(store);

        var result = await service.ValidateStasTransactionAsync(new string('c', 64));

        Assert.False(result.AskLater);
        Assert.False(result.IsLegal);
        Assert.Equal(TokenProjectionValidationStatus.Invalid, result.ValidationStatus);
        Assert.True(result.B2GResolved);
        Assert.Equal(["illegal-root-1"], result.IllegalRoots);
    }

    private static TransactionQueryService CreateService(Raven.Client.Documents.IDocumentStore store)
        => new(
            store,
            new TxLifecycleProjectionReader(store),
            new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store)));

    private static async Task SeedAsync(Raven.Client.Documents.IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);
        await session.SaveChangesAsync();
    }
}
