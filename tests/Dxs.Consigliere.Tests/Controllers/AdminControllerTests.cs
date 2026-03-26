using Dxs.Common.Cache;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminControllerTests
{
    [Fact]
    public void GetCacheStatus_ReturnsProjectionCacheMetrics()
    {
        var controller = new AdminController(new TestNetworkProvider());

        var result = controller.GetCacheStatus(new FakeProjectionReadCacheTelemetry());

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ProjectionCacheStatusResponse>(ok.Value);
        Assert.True(payload.Enabled);
        Assert.Equal("memory", payload.Backend);
        Assert.Equal(42, payload.Hits);
        Assert.Equal(9, payload.InvalidatedKeys);
    }

    private sealed class FakeProjectionReadCacheTelemetry : IProjectionReadCacheTelemetry
    {
        public ProjectionCacheStatsSnapshot GetSnapshot()
            => new(
                "memory",
                true,
                11,
                1024,
                42,
                18,
                21,
                9,
                4,
                2);
    }

    private sealed class TestNetworkProvider : Dxs.Consigliere.Services.INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
