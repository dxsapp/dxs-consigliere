#nullable enable
using System;

namespace Dxs.Bsv.P2p.Observer;

/// <summary>
/// Helpers for converting between BSV transaction-id byte orderings.
/// Wave 2 audit A2 H1: P2P wire frames carry txids in wire (little-
/// endian / hash) order; the rest of Consigliere — Bitails / JungleBus
/// realtime, OutgoingTransactionStore, TxLifecycleProjectionDocument,
/// and the audit-visible <see cref="BitcoinHelpers.GetTxId"/> helper —
/// uses display order (byte-reversed). Normalising at the runner
/// boundary lets observations from every source dedupe on the same
/// id and lets <c>SeenBySources</c> accumulate <c>"p2p"</c> alongside
/// <c>"bitails"</c> / <c>"junglebus"</c> for the same transaction.
/// </summary>
public static class TxHashOrder
{
    private const int TxidLength = 32;

    /// <summary>
    /// Convert a wire-order 32-byte tx hash (as seen in inv / getdata
    /// / reject items) to the canonical display-order hex string
    /// matching <see cref="BitcoinHelpers.GetTxId"/>.
    /// </summary>
    public static string WireToDisplayHex(ReadOnlySpan<byte> wire)
    {
        if (wire.Length != TxidLength)
            throw new ArgumentException($"wire txid must be {TxidLength} bytes", nameof(wire));
        Span<byte> reversed = stackalloc byte[TxidLength];
        for (var i = 0; i < TxidLength; i++) reversed[i] = wire[TxidLength - 1 - i];
        return Convert.ToHexString(reversed).ToLowerInvariant();
    }

    /// <summary>
    /// Inverse of <see cref="WireToDisplayHex"/>. Takes a canonical
    /// display-order hex string (64 chars, lowercase or uppercase)
    /// and returns the 32 wire-order bytes ready to be packed into
    /// an inv / getdata vector.
    /// </summary>
    public static byte[] DisplayHexToWire(string displayHex)
    {
        if (string.IsNullOrEmpty(displayHex) || displayHex.Length != TxidLength * 2)
            throw new ArgumentException("displayHex must be 64 hex characters", nameof(displayHex));
        var display = Convert.FromHexString(displayHex);
        var wire = new byte[TxidLength];
        for (var i = 0; i < TxidLength; i++) wire[i] = display[TxidLength - 1 - i];
        return wire;
    }
}
