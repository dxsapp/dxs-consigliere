using Dxs.Common.Cache;
using Dxs.Consigliere.Data.Cache;

namespace Dxs.Consigliere.Tests.Data.Cache;

public class ProjectionCacheInvalidationTelemetryTests
{
    [Fact]
    public void Record_ClassifiesTagsIntoStableDomains()
    {
        var telemetry = new ProjectionCacheInvalidationTelemetry();

        telemetry.Record(
        [
            new ProjectionCacheTag("address-history:addr-1"),
            new ProjectionCacheTag("address-balance:addr-2"),
            new ProjectionCacheTag("token-history:token-1"),
            new ProjectionCacheTag("tx-lifecycle:tx-1"),
            new ProjectionCacheTag("tracked-address-readiness:addr-1")
        ]);

        var snapshot = telemetry.GetSnapshot();

        Assert.Equal(1, snapshot.Calls);
        Assert.Equal(5, snapshot.Tags);
        Assert.Collection(
            snapshot.Domains.OrderBy(x => x.Domain, StringComparer.Ordinal),
            item =>
            {
                Assert.Equal("address", item.Domain);
                Assert.Equal(1, item.Calls);
                Assert.Equal(2, item.Tags);
            },
            item =>
            {
                Assert.Equal("token", item.Domain);
                Assert.Equal(1, item.Calls);
                Assert.Equal(1, item.Tags);
            },
            item =>
            {
                Assert.Equal("tracked_readiness", item.Domain);
                Assert.Equal(1, item.Calls);
                Assert.Equal(1, item.Tags);
            },
            item =>
            {
                Assert.Equal("tx_lifecycle", item.Domain);
                Assert.Equal(1, item.Calls);
                Assert.Equal(1, item.Tags);
            });
    }
}
