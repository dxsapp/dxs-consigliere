using Dxs.Bsv;
using Dxs.Bsv.BitcoinMonitor;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Dto.Responses.History;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminTrackedControllerTests
{
    [Fact]
    public async Task GetTrackedAddresses_ReturnsQueryResults()
    {
        var queryService = new Mock<IAdminTrackingQueryService>(MockBehavior.Strict);
        queryService.Setup(x => x.GetTrackedAddressesAsync(false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AdminTrackedAddressResponse
                {
                    Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                    Name = "Genesis",
                    Readiness = new TrackedEntityReadinessResponse
                    {
                        Tracked = true,
                        EntityType = TrackedEntityType.Address,
                        EntityId = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                        History = new TrackedHistoryStatusResponse { HistoryReadiness = TrackedEntityHistoryReadiness.ForwardLive }
                    }
                }
            ]);

        var controller = CreateController();

        var result = await controller.GetTrackedAddresses(false, queryService.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminTrackedAddressResponse[]>(ok.Value);
        Assert.Single(payload);
        Assert.Equal("Genesis", payload[0].Name);
    }

    [Fact]
    public async Task DeleteTrackedAddress_RejectsConfigManagedEntity()
    {
        var controller = CreateController(new TransactionFilterConfig
        {
            Addresses = ["1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"]
        });

        var result = await controller.DeleteTrackedAddress(
            "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
            Mock.Of<ITrackedEntityRegistrationStore>(MockBehavior.Strict),
            Mock.Of<ITransactionFilter>(MockBehavior.Strict));

        var conflict = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        Assert.NotNull(conflict.Value);
    }

    [Fact]
    public async Task DeleteTrackedToken_UntracksDbManagedEntityAndRemovesRuntimeWatch()
    {
        const string tokenId = "1111111111111111111111111111111111111111";
        var registrationStore = new Mock<ITrackedEntityRegistrationStore>(MockBehavior.Strict);
        var filter = new Mock<ITransactionFilter>(MockBehavior.Strict);

        registrationStore.Setup(x => x.UntrackTokenAsync(tokenId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        filter.Setup(x => x.UnmanageUtxoSetForToken(It.Is<TokenId>(v => v.Value == tokenId)));

        var controller = CreateController();

        var result = await controller.DeleteTrackedToken(tokenId, registrationStore.Object, filter.Object);

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var payload = Assert.IsType<AdminTrackedEntityDeleteResponse>(ok.Value);
        Assert.Equal("untracked", payload.Code);
        Assert.True(payload.Tombstoned);
        registrationStore.VerifyAll();
        filter.VerifyAll();
    }

    private static AdminTrackedController CreateController(TransactionFilterConfig? filterConfig = null)
        => new(
            new TestNetworkProvider(),
            Options.Create(filterConfig ?? new TransactionFilterConfig()));

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Network Network => Network.Mainnet;
    }
}
