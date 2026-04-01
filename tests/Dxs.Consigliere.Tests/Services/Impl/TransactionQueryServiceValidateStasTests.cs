using Dxs.Consigliere.Data.Journal;
using Dxs.Consigliere.Data.Models.Tokens;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.BackgroundTasks;
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
    public async Task ReturnsRepairSchedulingMetadataWhenUnknownVerdictSchedulesWork()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        await SeedAsync(store, new MetaTransaction
        {
            Id = new string('d', 64),
            IsStas = true,
            IsIssue = false,
            AllStasInputsKnown = false,
            MissingTransactions = ["missing-parent-1"],
            TokenIds = ["token-1"],
            IllegalRoots = [],
            StasValidationStatus = TokenProjectionValidationStatus.Unknown
        });

        var scheduler = new FakeValidationDependencyRepairScheduler();
        var service = CreateService(store, scheduler);

        var result = await service.ValidateStasTransactionAsync(new string('d', 64));

        Assert.Equal("pending", result.ValidationRepairState);
        Assert.Equal(1711965600000, result.ValidationRepairUpdatedAt);
        Assert.Equal([new string('d', 64)], scheduler.ScheduledTxIds);
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

    private static TransactionQueryService CreateService(
        Raven.Client.Documents.IDocumentStore store,
        IValidationDependencyRepairScheduler? scheduler = null)
        => new(
            store,
            new TxLifecycleProjectionReader(store),
            new TxLifecycleProjectionRebuilder(store, new RavenObservationJournalReader(store)),
            scheduler);

    private static async Task SeedAsync(Raven.Client.Documents.IDocumentStore store, MetaTransaction transaction)
    {
        using var session = store.OpenAsyncSession();
        await session.StoreAsync(transaction, transaction.Id);
        await session.SaveChangesAsync();
    }

    private sealed class FakeValidationDependencyRepairScheduler : IValidationDependencyRepairScheduler
    {
        public List<string> ScheduledTxIds { get; } = [];

        public Task<ValidationRepairWorkItemDocument?> ScheduleTransactionAsync(string txId, string reason, CancellationToken cancellationToken = default)
        {
            ScheduledTxIds.Add(txId);
            return Task.FromResult<ValidationRepairWorkItemDocument?>(new ValidationRepairWorkItemDocument
            {
                Id = ValidationRepairWorkItemDocument.GetId(txId),
                EntityId = txId,
                State = ValidationRepairStates.Pending,
                Reasons = [reason],
                CreatedAt = 1711965600000,
                UpdatedAt = 1711965600000
            });
        }

        public Task<ValidationRepairWorkItemDocument?> GetScheduledTransactionAsync(string txId, CancellationToken cancellationToken = default)
            => Task.FromResult<ValidationRepairWorkItemDocument?>(null);

        public Task CancelTransactionAsync(string txId, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
