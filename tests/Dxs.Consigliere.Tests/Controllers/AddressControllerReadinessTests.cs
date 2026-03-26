using Dxs.Bsv;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AddressControllerReadinessTests
{
    [Fact]
    public async Task BalanceReturnsConflictWhenTrackedAddressIsNotReadable()
    {
        var readiness = new Mock<ITrackedEntityReadinessService>();
        readiness.Setup(x => x.GetBlockingReadinessAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessGateResponse
            {
                Entities = [new TrackedEntityReadinessResponse
                {
                    Tracked = true,
                    EntityType = "address",
                    EntityId = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa",
                    LifecycleStatus = "backfilling"
                }]
            });

        var utxoManager = new Mock<IUtxoManager>(MockBehavior.Strict);
        var controller = new AddressController();

        var result = await controller.GetBalance(
            new BalanceRequest(["1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa"], []),
            readiness.Object,
            utxoManager.Object,
            CancellationToken.None
        );

        var conflict = Assert.IsType<ObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        var payload = Assert.IsType<TrackedEntityReadinessGateResponse>(conflict.Value);
        Assert.Equal("scope_not_ready", payload.Code);
        Assert.Single(payload.Entities);
    }

    [Fact]
    public async Task HistoryReturnsPayloadWhenReadinessAllows()
    {
        var readiness = new Mock<ITrackedEntityReadinessService>();
        readiness.Setup(x => x.GetBlockingHistoryReadinessAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedEntityReadinessGateResponse)null!);
        readiness.Setup(x => x.GetAddressReadinessAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessResponse());

        var history = new Mock<IAddressHistoryService>();
        history.Setup(x => x.GetHistory(It.IsAny<GetAddressHistoryRequest>()))
            .ReturnsAsync(new Dxs.Consigliere.Dto.Responses.AddressHistoryResponse());

        var controller = new AddressController();

        var result = await controller.GetDetailedHistory(
            new GetAddressHistoryRequest("1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa", [], false, false, 0, 10),
            readiness.Object,
            history.Object,
            new TestNetworkProvider(),
            CancellationToken.None
        );

        Assert.IsType<OkObjectResult>(result);
    }

    private sealed class TestNetworkProvider : INetworkProvider
    {
        public Dxs.Bsv.Network Network => Dxs.Bsv.Network.Mainnet;
    }
}
