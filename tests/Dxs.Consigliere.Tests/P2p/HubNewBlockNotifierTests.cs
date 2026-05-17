using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.Services.P2p;
using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;

using Moq;

namespace Dxs.Consigliere.Tests.P2p;

/// <summary>
/// Wave 1 S5. Verifies <see cref="HubNewBlockNotifier"/> emits the new
/// tip to the <c>block:tip</c> group via the strongly-typed hub context.
/// Live SignalR end-to-end coverage is exercised by the S3 service tests
/// that drive INewBlockNotifier through HeadersChainService; here we
/// pin the group name and DTO routing so a refactor of either can't
/// silently misroute the tip.
/// </summary>
public class HubNewBlockNotifierTests
{
    [Fact]
    public async Task NotifyAsync_BroadcastsToBlockTipGroup_WithGivenDto()
    {
        var captured = new Mock<IWalletHub>(MockBehavior.Strict);
        captured.Setup(c => c.OnNewBlock(It.IsAny<BlockTipDto>())).Returns(Task.CompletedTask);

        var clients = new Mock<IHubClients<IWalletHub>>(MockBehavior.Strict);
        clients.Setup(c => c.Group("block:tip")).Returns(captured.Object);

        var hub = new Mock<IHubContext<WalletHub, IWalletHub>>(MockBehavior.Strict);
        hub.SetupGet(h => h.Clients).Returns(clients.Object);

        var sut = new HubNewBlockNotifier(hub.Object);
        var tip = new BlockTipDto("abc", 42, 1700000000000L, "def", 80);

        await sut.NotifyAsync(tip, CancellationToken.None);

        clients.Verify(c => c.Group("block:tip"), Times.Once);
        captured.Verify(c => c.OnNewBlock(tip), Times.Once);
    }
}
