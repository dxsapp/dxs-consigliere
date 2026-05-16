using Dxs.Bsv.Models;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Services;

public interface IBroadcastService
{
    Task<decimal> SatoshisPerByte();

    Task<Broadcast> Broadcast(string raw, string batchId = null);
    Task<Broadcast> Broadcast(Transaction transaction, string batchId = null);

    /// <summary>
    /// Gate 3 P2P broadcast with lifecycle tracking.
    /// Returns a receipt immediately after persistence; state transitions
    /// are streamed via SignalR OnBroadcastStateChanged.
    /// </summary>
    Task<BroadcastReceipt> SubmitAsync(string rawHex, string clientConnectionId = null, CancellationToken ct = default);
}

/// <summary>Immediate receipt returned by <see cref="IBroadcastService.SubmitAsync"/>.</summary>
public sealed record BroadcastReceipt(
    string TxId,
    OutgoingTxState State,
    long CreatedAtMs,
    string FailReason = null)
{
    public bool IsTerminal => OutgoingTxStates.IsTerminal(State);
}
