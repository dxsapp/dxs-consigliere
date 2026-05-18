using Dxs.Consigliere.Data.Models.P2p;

namespace Dxs.Consigliere.Services;

public interface IBroadcastService
{
    Task<decimal> SatoshisPerByte();

    /// <summary>
    /// Wave 5 — the canonical broadcast entrypoint. Validates the raw
    /// tx, persists an <c>OutgoingTransaction</c>, and announces it via
    /// <see cref="P2p.TxRelayCoordinator.AnnounceAsync"/> in the
    /// background. Returns a receipt immediately after persistence; the
    /// lifecycle worker streams state transitions via SignalR
    /// <c>OnBroadcastStateChanged</c>.
    /// <para>S0 renamed it from the prior <c>SubmitAsync</c>; S2
    /// removed the legacy multi-provider <c>Broadcast(string)</c> /
    /// <c>Broadcast(Transaction)</c> overloads. There is no HTTP
    /// fallback — <see cref="P2p.TxRelayCoordinator"/> is the only
    /// announce path.</para>
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
