using System.Threading.Tasks;

using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Data.P2p;

/// <summary>
/// Pushes an <see cref="OutgoingTransaction"/>'s current lifecycle state to
/// clients watching that txid (SignalR group <c>broadcast:{txId}</c>, the
/// hub's <c>OnBroadcastStateChanged</c> event). Implemented in the WebSockets
/// layer; <see cref="OutgoingTransactionStore"/> depends only on this
/// interface so the data layer stays free of a SignalR dependency. Every
/// persisted state transition (Validated → Dispatching → PeerAcked →
/// PeerRelayed → MempoolSeen → Mined → Confirmed, plus failures) flows
/// through the store's SaveAsync, so emitting there makes the Broadcast
/// inspector stepper advance live.
/// </summary>
public interface IBroadcastStateNotifier
{
    Task NotifyStateAsync(OutgoingTransaction tx);
}
