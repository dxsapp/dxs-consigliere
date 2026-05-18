using Dxs.Bsv.Models;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Services;

public interface IBroadcastService
{
    Task<decimal> SatoshisPerByte();

    // W5: legacy multi-provider broadcast — slated for removal in S2.
    // Kept transitionally so unrelated callers compile while S0+S1 land.
    Task<Broadcast> Broadcast(string raw, string batchId = null);
    Task<Broadcast> Broadcast(Transaction transaction, string batchId = null);

    /// <summary>
    /// Wave 5 S0 — the canonical broadcast entrypoint. Validates the raw
    /// tx, persists an <c>OutgoingTransaction</c>, and announces it via
    /// <see cref="P2p.TxRelayCoordinator.AnnounceAsync"/> in the
    /// background. Returns a receipt immediately after persistence; the
    /// lifecycle worker streams state transitions via SignalR
    /// <c>OnBroadcastStateChanged</c>.
    /// <para>Renamed from the previous <c>SubmitAsync</c> per the W5
    /// "single broadcast method" mandate. No HTTP fallback —
    /// <see cref="P2p.TxRelayCoordinator"/> is the only announce
    /// path.</para>
    /// </summary>
    Task<BroadcastReceipt> BroadcastAsync(
        string rawHex,
        string clientConnectionId = null,
        CancellationToken ct = default);
}

/// <summary>Immediate receipt returned by <see cref="IBroadcastService.BroadcastAsync"/>.</summary>
public sealed record BroadcastReceipt(
    string TxId,
    OutgoingTxState State,
    long CreatedAtMs,
    string FailReason = null)
{
    public bool IsTerminal => OutgoingTxStates.IsTerminal(State);
}
