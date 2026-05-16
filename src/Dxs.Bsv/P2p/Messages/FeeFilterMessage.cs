#nullable enable
using System;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// <c>feefilter</c>: peer tells us its minimum-fee-per-1000-bytes for tx
/// relay. We must NOT announce a tx with fee/1000B below this value to
/// that peer.
/// Wire format: single int64 LE (satoshis per kilobyte).
/// </summary>
public sealed record FeeFilterMessage(long FeePerKbSat)
{
    public byte[] Serialize()
    {
        var w = new P2pWriter(8);
        w.WriteInt64Le(FeePerKbSat);
        return w.ToArray();
    }

    public static FeeFilterMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        return new FeeFilterMessage(reader.ReadInt64Le("feefilter.fee_per_kb"));
    }
}
