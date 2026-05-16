namespace Dxs.Consigliere.WebSockets;

/// <summary>SignalR-friendly receipt returned by BroadcastTracked.</summary>
public sealed record BroadcastReceiptDto(
    string TxId,
    string State,
    long CreatedAtMs,
    string FailReason = null);
