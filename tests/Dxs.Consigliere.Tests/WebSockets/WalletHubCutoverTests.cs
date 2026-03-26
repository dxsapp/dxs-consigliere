using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Dto.Responses.Readiness;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.WebSockets;

using MediatR;

using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging.Abstractions;

using Moq;

namespace Dxs.Consigliere.Tests.WebSockets;

public class WalletHubCutoverTests
{
    private const string Address = "1A1zP1eP5QGefi2DMPTfTL5SLmv7DivfNa";
    private const string TokenId = "1111111111111111111111111111111111111111";

    [Fact]
    public async Task GetBalance_ThrowsWhenTrackedScopeIsNotReadable()
    {
        var readiness = new Mock<ITrackedEntityReadinessService>(MockBehavior.Strict);
        readiness.Setup(x => x.GetBlockingReadinessAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessGateResponse
            {
                Code = "scope_not_ready",
                Entities =
                [
                    new TrackedEntityReadinessResponse
                    {
                        Tracked = true,
                        EntityType = "address",
                        EntityId = Address,
                        LifecycleStatus = "backfilling"
                    }
                ]
            });

        var utxoManager = new Mock<IUtxoManager>(MockBehavior.Strict);
        var hub = BuildHub();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => hub.GetBalance(
            new BalanceRequest([Address], [TokenId]),
            readiness.Object,
            utxoManager.Object
        ));

        Assert.Equal("tracked scope is not live yet", exception.Message);
    }

    [Fact]
    public async Task GetUtxoSet_DelegatesToManagerWhenReadinessAllows()
    {
        var readiness = new Mock<ITrackedEntityReadinessService>(MockBehavior.Strict);
        readiness.Setup(x => x.GetBlockingReadinessAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedEntityReadinessGateResponse)null!);

        var expected = new GetUtxoSetResponse([]);
        var utxoManager = new Mock<IUtxoManager>(MockBehavior.Strict);
        utxoManager.Setup(x => x.GetUtxoSet(It.IsAny<GetUtxoSetRequest>()))
            .ReturnsAsync(expected);

        var hub = BuildHub();
        var result = await hub.GetUtxoSet(new GetUtxoSetRequest(TokenId, Address, null), readiness.Object, utxoManager.Object);

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GetHistory_ThrowsWhenScopeIsUntracked()
    {
        var readiness = new Mock<ITrackedEntityReadinessService>(MockBehavior.Strict);
        readiness.Setup(x => x.GetBlockingReadinessAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TrackedEntityReadinessGateResponse
            {
                Code = "not_tracked",
                Entities =
                [
                    new TrackedEntityReadinessResponse
                    {
                        Tracked = false,
                        EntityType = "address",
                        EntityId = Address
                    }
                ]
            });

        var history = new Mock<IAddressHistoryService>(MockBehavior.Strict);
        var hub = BuildHub();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => hub.GetHistory(
            new GetAddressHistoryRequest(Address, [TokenId], false, false, 0, 10),
            readiness.Object,
            history.Object
        ));

        Assert.Equal("tracked scope is required before reading this stream", exception.Message);
    }

    private static WalletHub BuildHub()
    {
        var connectionManager = new Mock<IConnectionManager>(MockBehavior.Loose);
        var publisher = new Mock<IPublisher>(MockBehavior.Loose);

        return new WalletHub(
            connectionManager.Object,
            publisher.Object,
            NullLogger<WalletHub>.Instance
        );
    }
}
