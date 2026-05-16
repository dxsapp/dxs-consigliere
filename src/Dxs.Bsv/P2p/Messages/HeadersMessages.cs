#nullable enable
using System;
using System.Collections.Generic;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// <c>getheaders</c> payload: protocol version + locator hashes + stop hash.
/// We never request headers, but we MUST parse incoming requests properly
/// (per audit M1 — silent empty reply only after successful parse).
/// </summary>
public sealed record GetHeadersMessage(uint ProtocolVersion, IReadOnlyList<byte[]> Locator, byte[] StopHash)
{
    /// <summary>Per <c>net.h::MAX_LOCATOR_SZ</c>.</summary>
    public const int MaxLocatorEntries = 101;

    public byte[] Serialize()
    {
        var w = new P2pWriter();
        w.WriteUInt32Le(ProtocolVersion);
        w.WriteVarInt((ulong)Locator.Count);
        foreach (var hash in Locator)
        {
            if (hash.Length != 32)
                throw new InvalidOperationException("Locator hash must be 32 bytes");
            w.WriteBytes(hash);
        }
        if (StopHash.Length != 32)
            throw new InvalidOperationException("StopHash must be 32 bytes");
        w.WriteBytes(StopHash);
        return w.ToArray();
    }

    public static GetHeadersMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        var protocolVersion = reader.ReadUInt32Le("getheaders.protocol_version");
        var count = reader.ReadVarInt("getheaders.locator_count");
        if (count > (ulong)MaxLocatorEntries)
            throw new P2pDecodeException($"getheaders locator count {count} exceeds max {MaxLocatorEntries}");
        var locator = new List<byte[]>((int)count);
        for (ulong i = 0; i < count; i++)
            locator.Add(reader.ReadBytes(32, $"getheaders.locator[{i}]").ToArray());
        var stopHash = reader.ReadBytes(32, "getheaders.stop_hash").ToArray();
        return new GetHeadersMessage(protocolVersion, locator, stopHash);
    }
}

/// <summary>
/// One 80-byte block header plus a trailing tx-count varint (always 0 for
/// the <c>headers</c> message — BIP 152 retains the field for compatibility).
/// </summary>
public sealed record BlockHeader(byte[] Bytes80)
{
    public const int Size = 80;
}

/// <summary>
/// <c>headers</c>: array of block headers. We send empty (0-count) replies
/// to every <c>getheaders</c> and parse incoming (drop content).
/// </summary>
public sealed record HeadersMessage(IReadOnlyList<BlockHeader> Headers)
{
    /// <summary>Per <c>net.h::MAX_HEADERS_RESULTS</c>.</summary>
    public const int MaxHeaders = 2000;

    public byte[] Serialize()
    {
        var w = new P2pWriter(1 + Headers.Count * (BlockHeader.Size + 1));
        w.WriteVarInt((ulong)Headers.Count);
        foreach (var h in Headers)
        {
            if (h.Bytes80.Length != BlockHeader.Size)
                throw new InvalidOperationException("BlockHeader must be 80 bytes");
            w.WriteBytes(h.Bytes80);
            w.WriteVarInt(0); // tx_count, always zero on the wire
        }
        return w.ToArray();
    }

    public static HeadersMessage Parse(ReadOnlySpan<byte> payload)
    {
        var reader = new P2pReader(payload);
        var count = reader.ReadVarInt("headers.count");
        if (count > (ulong)MaxHeaders)
            throw new P2pDecodeException($"headers count {count} exceeds max {MaxHeaders}");
        var headers = new List<BlockHeader>((int)count);
        for (ulong i = 0; i < count; i++)
        {
            var bytes = reader.ReadBytes(BlockHeader.Size, $"headers[{i}].block_header").ToArray();
            // Discard tx_count (always 0 on the wire per BIP).
            _ = reader.ReadVarInt($"headers[{i}].tx_count");
            headers.Add(new BlockHeader(bytes));
        }
        return new HeadersMessage(headers);
    }

    /// <summary>Empty <c>headers</c> response — the standard answer to any incoming <c>getheaders</c>.</summary>
    public static HeadersMessage Empty { get; } = new(Array.Empty<BlockHeader>());
}
