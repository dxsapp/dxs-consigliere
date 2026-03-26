using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Responses.History;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class ReadinessControllerTests
{
    [Fact]
    public async Task ReturnsAddressReadinessPayload()
    {
        const string address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
        var service = new Mock<ITrackedEntityReadinessService>();
        service.Setup(x => x.GetAddressReadinessAsync(address, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessResponse
            {
                Tracked = true,
                EntityType = "address",
                EntityId = address,
                LifecycleStatus = "backfilling"
            });

        var controller = new ReadinessController(new TestNetworkProvider());

        var result = await controller.GetAddressReadiness(address, service.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TrackedEntityReadinessResponse>(ok.Value);
        Assert.True(payload.Tracked);
        Assert.Equal("backfilling", payload.LifecycleStatus);
    }

    [Fact]
    public async Task ReturnsTokenReadinessPayload()
    {
        const string tokenId = "1111111111111111111111111111111111111111";
        var service = new Mock<ITrackedEntityReadinessService>();
        service.Setup(x => x.GetTokenReadinessAsync(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessResponse
            {
                Tracked = true,
                EntityType = "token",
                EntityId = tokenId,
                LifecycleStatus = "live",
                Readable = true,
                Authoritative = true
            });

        var controller = new ReadinessController(new TestNetworkProvider());

        var result = await controller.GetTokenReadiness(tokenId, service.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TrackedEntityReadinessResponse>(ok.Value);
        Assert.True(payload.Readable);
        Assert.True(payload.Authoritative);
    }

    [Fact]
    public async Task ReturnsRootedTokenHistoryStatusWhenPresent()
    {
        const string tokenId = "1111111111111111111111111111111111111111";
        var service = new Mock<ITrackedEntityReadinessService>();
        service.Setup(x => x.GetTokenReadinessAsync(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessResponse
            {
                Tracked = true,
                EntityType = "token",
                EntityId = tokenId,
                LifecycleStatus = "live",
                Readable = true,
                Authoritative = true,
                History = new TrackedHistoryStatusResponse
                {
                    HistoryReadiness = "full_history_live",
                    RootedToken = new RootedTokenHistoryStatusResponse
                    {
                        TrustedRoots = ["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"],
                        TrustedRootCount = 1,
                        CompletedTrustedRootCount = 1,
                        RootedHistorySecure = true
                    }
                }
            });

        var controller = new ReadinessController(new TestNetworkProvider());

        var result = await controller.GetTokenReadiness(tokenId, service.Object, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<TrackedEntityReadinessResponse>(ok.Value);
        Assert.NotNull(payload.History);
        Assert.NotNull(payload.History.RootedToken);
        Assert.True(payload.History.RootedToken.RootedHistorySecure);
        Assert.Equal(1, payload.History.RootedToken.TrustedRootCount);
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
