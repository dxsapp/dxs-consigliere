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
        BroadcastSource source,
        string clientConnectionId = null,
        CancellationToken ct = default);
}

/// <summary>
/// wave-A3 S3-followup-2 — provenance of a broadcast. The audit
/// trail (S3) records this so a system auto-rebroadcast is no
/// longer indistinguishable from (and falsely attributed to) an
/// operator-initiated one. Only <see cref="Operator"/>
/// broadcasts fail-stop on audit-write failure; the others are
/// audited best-effort so a degraded audit store can't halt the
/// wallet path or the background rebroadcast monitor.
/// </summary>
public enum BroadcastSource
{
    /// <summary>Authenticated admin via the UI / admin API.</summary>
    Operator,
    /// <summary>Unauthenticated external client on `POST /api/tx/broadcast`.</summary>
    Api,
    /// <summary>Wallet client over the SignalR hub.</summary>
    Wallet,
    /// <summary>Background task (e.g. UnconfirmedTransactionsMonitor) — no human actor.</summary>
    System,
}

public static class BroadcastSourceExtensions
{
    /// <summary>Stable lowercase wire string for the audit `source` field.</summary>
    public static string ToWire(this BroadcastSource source) => source switch
    {
        BroadcastSource.Operator => "operator",
        BroadcastSource.Api => "api",
        BroadcastSource.Wallet => "wallet",
        BroadcastSource.System => "system",
        _ => "unknown",
    };
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
