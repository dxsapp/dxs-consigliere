using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Data.Transactions;
using Dxs.Consigliere.Services;
using Dxs.Tests.Shared;
using Raven.TestDriver;

namespace Dxs.Consigliere.Tests.Data.Runtime;

public class ValidationRepairStatusReaderIntegrationTests : RavenTestDriver
{
    [Fact]
    public async Task GetSnapshotAsync_SurfacesStopReasonAndTraversalMetadata()
    {
        if (!DotNetRuntimeFacts.HasRuntimeMajor(8))
            return;

        using var store = GetDocumentStore();
        var workStore = new ValidationRepairWorkItemStore(store);
        var reader = new ValidationRepairStatusReader(store);

        await workStore.ScheduleAsync("tx-1", ValidationRepairReasons.PublicValidate, ["dep-a"]);
        await workStore.MarkRetryAsync(
            "tx-1",
            ["dep-a"],
            "validation dependency unresolved",
            DateTimeOffset.UtcNow.AddMinutes(1),
            failed: false,
            new ValidationDependencyResolutionResult(
                ["dep-a"],
                ["dep-b"],
                "validation dependency unresolved",
                ValidationRepairStopReasons.MissingDependency,
                1,
                2,
                1));

        var snapshot = await reader.GetSnapshotAsync();

        Assert.Equal(1, snapshot.TotalCount);
        Assert.Single(snapshot.Items);
        var item = snapshot.Items[0];
        Assert.Equal(ValidationRepairStopReasons.MissingDependency, item.LastStopReason);
        Assert.Equal(1, item.LastFetchCount);
        Assert.Equal(2, item.LastVisitedCount);
        Assert.Equal(1, item.LastTraversalDepth);
    }
}
