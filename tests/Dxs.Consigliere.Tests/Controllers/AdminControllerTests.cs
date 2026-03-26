using Dxs.Common.Cache;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Addresses;
using Dxs.Consigliere.Data.Cache;
using Dxs.Consigliere.Dto.Responses;

using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminControllerTests
{
    [Fact]
    public async Task GetCacheStatus_ReturnsProjectionCacheMetrics()
    {
        var controller = new AdminController(new TestNetworkProvider());

        var result = await controller.GetCacheStatus(new FakeProjectionReadCacheTelemetry(), new FakeProjectionCacheRuntimeStatusReader());

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ProjectionCacheStatusResponse>(ok.Value);
        Assert.True(payload.Enabled);
        Assert.Equal("memory", payload.Backend);
        Assert.Equal(42, payload.Hits);
        Assert.Equal(9, payload.InvalidatedKeys);
        Assert.Equal(2, payload.Invalidation.Domains.Length);
        Assert.Equal(4, payload.ProjectionLag.Token.Lag);
        Assert.Equal(7, payload.HistoryEnvelopeBackfill.PendingCount);
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

    private sealed class FakeProjectionCacheRuntimeStatusReader : IProjectionCacheRuntimeStatusReader
    {
        public Task<ProjectionCacheRuntimeStatusSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(
                new ProjectionCacheRuntimeStatusSnapshot(
                    new ProjectionLagSnapshot(
                        25,
                        new ProjectionLagItemSnapshot("address", 21, 4),
                        new ProjectionLagItemSnapshot("token", 21, 4),
                        new ProjectionLagItemSnapshot("tx_lifecycle", 24, 1)),
                    new ProjectionCacheInvalidationTelemetrySnapshot(
                        5,
                        11,
                        DateTimeOffset.Parse("2026-03-26T18:10:00+00:00"),
                        [
                            new ProjectionCacheInvalidationDomainSnapshot("address", 3, 7, DateTimeOffset.Parse("2026-03-26T18:10:00+00:00")),
                            new ProjectionCacheInvalidationDomainSnapshot("tracked_readiness", 2, 4, DateTimeOffset.Parse("2026-03-26T18:10:05+00:00"))
                        ]),
                    new AddressHistoryEnvelopeBackfillTelemetrySnapshot(
                        20,
                        13,
                        1,
                        7,
                        301,
                        DateTimeOffset.Parse("2026-03-26T18:11:00+00:00"),
                        DateTimeOffset.Parse("2026-03-26T18:11:03+00:00"))));
    }
}
