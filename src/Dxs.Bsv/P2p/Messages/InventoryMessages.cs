#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

using Dxs.Bsv.P2p.Codec;

namespace Dxs.Bsv.P2p.Messages;

/// <summary>
/// Bitcoin inventory vector type. Standard values per BSV
/// <c>protocol.h::GetDataMsg</c>.
/// </summary>
public enum InvType : uint
{
    Error               = 0,
    Tx                  = 1,
    Block               = 2,
    FilteredBlock       = 3,
    CompactBlock        = 4,
    DataRefTx           = 5,
}

/// <summary>
/// One entry of an <c>inv</c>/<c>getdata</c>/<c>notfound</c> payload:
/// 4-byte type tag + 32-byte hash (txid or block hash).
/// </summary>
public sealed record InvVector(InvType Type, byte[] Hash)
{
    public const int Size = 4 + 32;

    public void Write(P2pWriter writer)
    {
        if (Hash is null || Hash.Length != 32)
            throw new InvalidOperationException("InvVector.Hash must be 32 bytes");
        writer.WriteUInt32Le((uint)Type);
        writer.WriteBytes(Hash);
    }

    public static InvVector Read(ref P2pReader reader, string fieldName = "inv_vector")
    {
        var type = (InvType)reader.ReadUInt32Le($"{fieldName}.type");
        var hash = reader.ReadBytes(32, $"{fieldName}.hash").ToArray();
        return new InvVector(type, hash);
    }
}

internal static class InvVectorList
{
    /// <summary>Per <c>net.h::MAX_INV_SZ</c> in BSV-Node.</summary>
    public const int MaxItems = 50_000;

    public static byte[] Serialize(IReadOnlyList<InvVector> items)
    {
        var w = new P2pWriter(1 + items.Count * InvVector.Size);
        w.WriteVarInt((ulong)items.Count);
        foreach (var item in items)
            item.Write(w);
        return w.ToArray();
    }

    public static List<InvVector> Parse(ReadOnlySpan<byte> payload, string fieldName)
    {
        var reader = new P2pReader(payload);
        var count = reader.ReadVarInt($"{fieldName}.count");
        if (count > (ulong)MaxItems)
            throw new P2pDecodeException($"{fieldName} count {count} exceeds max {MaxItems}");
        var list = new List<InvVector>((int)count);
        for (ulong i = 0; i < count; i++)
            list.Add(InvVector.Read(ref reader, $"{fieldName}[{i}]"));
        return list;
    }
}

/// <summary>
/// <c>inv</c>: announce that we have one or more inventory items. The
/// receiver may then ask for them via <c>getdata</c>.
/// </summary>
public sealed record InvMessage(IReadOnlyList<InvVector> Items)
{
    public byte[] Serialize() => InvVectorList.Serialize(Items);

    public static InvMessage Parse(ReadOnlySpan<byte> payload) =>
        new(InvVectorList.Parse(payload, "inv"));

    public static InvMessage ForTx(byte[] txid) =>
        new(new[] { new InvVector(InvType.Tx, txid) });
}

/// <summary>
/// <c>getdata</c>: request the bodies of one or more inventory items.
/// Same wire format as <see cref="InvMessage"/>.
/// </summary>
public sealed record GetDataMessage(IReadOnlyList<InvVector> Items)
{
    public byte[] Serialize() => InvVectorList.Serialize(Items);

    public static GetDataMessage Parse(ReadOnlySpan<byte> payload) =>
        new(InvVectorList.Parse(payload, "getdata"));
}

/// <summary>
/// <c>notfound</c>: peer tells us it does not have the items we requested.
/// Same wire format as <see cref="InvMessage"/>.
/// </summary>
public sealed record NotFoundMessage(IReadOnlyList<InvVector> Items)
{
    public byte[] Serialize() => InvVectorList.Serialize(Items);

    public static NotFoundMessage Parse(ReadOnlySpan<byte> payload) =>
        new(InvVectorList.Parse(payload, "notfound"));
}
