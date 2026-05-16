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

    /// <summary>
    /// Wave 1: fired when the P2P headers chain advances to a new tip.
    /// Subscribe via SubscribeToBlockTip(). Frozen by S0.8.
    /// </summary>
    Task OnNewBlock(BlockTipDto tip);

    /// <summary>
    /// Wave 1: signature frozen by S0.8; emitter wired in Wave 3.
    /// Subscribe via SubscribeToReorg(). Never invoked in W1.
    /// </summary>
    Task OnReorg(ReorgEventDto reorg);
}

public sealed record BroadcastStateEvent(
    string TxId,
    OutgoingTxState State,
    long UpdatedAtMs,
    string FailReason = null);
