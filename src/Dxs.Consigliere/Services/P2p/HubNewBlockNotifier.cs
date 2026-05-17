using System.Threading;
using System.Threading.Tasks;

using Dxs.Consigliere.WebSockets;

using Microsoft.AspNetCore.SignalR;

namespace Dxs.Consigliere.Services.P2p;

/// <summary>
/// Wave 1 S5. Implements <see cref="INewBlockNotifier"/> by broadcasting
/// the new tip to the SignalR <c>block:tip</c> group via the strongly-
/// typed hub context. Reads from the frozen S0 surface:
/// - <see cref="IWalletHub.OnNewBlock"/> client callback
/// - <see cref="WalletHub.SubscribeToBlockTip"/> server method adds
///   callers to the group
/// </summary>
public sealed class HubNewBlockNotifier(IHubContext<WalletHub, IWalletHub> hub) : INewBlockNotifier
{
    public Task NotifyAsync(BlockTipDto tip, CancellationToken ct)
        => hub.Clients.Group("block:tip").OnNewBlock(tip);
}
