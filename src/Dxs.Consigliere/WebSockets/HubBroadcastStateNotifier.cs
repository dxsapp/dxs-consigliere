using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Data.P2p;

using Microsoft.AspNetCore.SignalR;

namespace Dxs.Consigliere.WebSockets;

/// <summary>
/// SignalR-backed <see cref="IBroadcastStateNotifier"/>: emits
/// <c>OnBroadcastStateChanged</c> to the <c>broadcast:{txId}</c> group that
/// the Broadcast inspector joins via <c>SubscribeToBroadcast</c>. Mirrors
/// <see cref="Dxs.Consigliere.Services.P2p.HubNewBlockNotifier"/>. The hub's
/// JSON protocol camelCases properties and serializes the
/// <see cref="OutgoingTxState"/> enum as a string, so the wire payload
/// matches the admin-ui <c>OnBroadcastStateChanged</c> event shape exactly.
/// </summary>
public sealed class HubBroadcastStateNotifier(IHubContext<WalletHub, IWalletHub> hub)
    : IBroadcastStateNotifier
{
    public Task NotifyStateAsync(OutgoingTransaction tx)
        => hub.Clients
            .Group($"broadcast:{tx.TxId}")
            .OnBroadcastStateChanged(new BroadcastStateEvent(
                tx.TxId,
                tx.State,
                tx.UpdatedAtMs,
                tx.LastError ?? tx.TerminalReason));
}
