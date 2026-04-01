using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Tests.Shared;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Transactions;

public class ValidationRepairWorkItemStoreIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task ScheduleAsync_NormalizesDependenciesAndMergesReasons()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new ValidationRepairWorkItemStore(store);

        await sut.ScheduleAsync("tx-1", ValidationRepairReasons.PublicValidate, ["b", "a", "a"]);
        var document = await sut.ScheduleAsync("tx-1", ValidationRepairReasons.MissingParentRepair, ["c", "a"]);

        Assert.Equal(ValidationRepairStates.Pending, document.State);
        Assert.Equal(["public_validate", "missing_parent_repair"], document.Reasons);
        Assert.Equal(["a", "c"], document.MissingDependencies);
        Assert.NotNull(document.NextAttemptAt);
    }

    [Fact]
    public async Task MarkRunningRetryResolvedAndBlocked_AdvanceLifecycle()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var sut = new ValidationRepairWorkItemStore(store);
        await sut.ScheduleAsync("tx-2", ValidationRepairReasons.PublicValidate, ["dep-1"]);

        var running = await sut.MarkRunningAsync("tx-2", ["dep-1"]);
        Assert.Equal(ValidationRepairStates.Running, running.State);
        Assert.Equal(1, running.AttemptCount);
        Assert.Null(running.NextAttemptAt);

        var retry = await sut.MarkRetryAsync("tx-2", ["dep-1", "dep-2"], "still_missing", DateTimeOffset.UtcNow.AddMinutes(1), failed: false);
        Assert.NotNull(retry);
        Assert.Equal(ValidationRepairStates.Pending, retry!.State);
        Assert.Equal(["dep-1", "dep-2"], retry.MissingDependencies);
        Assert.Equal("still_missing", retry.LastError);
        Assert.NotNull(retry.NextAttemptAt);

        var blocked = await sut.MarkBlockedAsync("tx-2", ["dep-1"], "target_transaction_missing");
        Assert.NotNull(blocked);
        Assert.Equal(ValidationRepairStates.Blocked, blocked!.State);
        Assert.Equal("target_transaction_missing", blocked.LastError);
        Assert.Null(blocked.NextAttemptAt);

        var resolved = await sut.MarkResolvedAsync("tx-2");
        Assert.NotNull(resolved);
        Assert.Equal(ValidationRepairStates.Resolved, resolved!.State);
        Assert.Empty(resolved.MissingDependencies);
        Assert.Null(resolved.LastError);
    }
}
