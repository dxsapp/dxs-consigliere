namespace Dxs.Consigliere.WebSockets;

/// <summary>
/// SignalR-friendly receipt returned by BroadcastTracked (and by W5's
/// unified Broadcast hub method).
///
/// FROZEN by the Wave 1 contract freeze (S0.9): property names, types,
/// and arity must not change without a contract-freeze amendment slice
/// in docs/stream-tasks/bsv-headers-chain-wave/. The DTO is frozen even
/// though the server-method signatures
/// (<see cref="IWalletServer.Broadcast"/>, BroadcastTracked) are NOT
/// frozen — W5 is authorised to collapse those two server methods into
/// a single Broadcast(hex) → BroadcastReceiptDto returning this exact
/// shape.
/// </summary>
public sealed record BroadcastReceiptDto(
    string TxId,
    string State,
    long CreatedAtMs,
    string FailReason = null);
