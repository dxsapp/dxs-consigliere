using Dxs.Consigliere.Data.Models.P2p;
using Dxs.Consigliere.Dto;
using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.WebSockets;

public interface IWalletHub
{
    Task OnTransactionFound(string hex);
    Task OnTransactionDeleted(string hash);
    Task OnBalanceChanged(BalanceDto balanceDto);
    Task OnRealtimeEvent(RealtimeEventResponse eventDto);

    /// <summary>
    /// Gate 3: fired whenever a tracked outgoing transaction changes state.
    /// Subscribe via SubscribeToBroadcast(txId).
    /// </summary>
    Task OnBroadcastStateChanged(BroadcastStateEvent evt);
}

public sealed record BroadcastStateEvent(
    string TxId,
    OutgoingTxState State,
    long UpdatedAtMs,
    string FailReason = null);
