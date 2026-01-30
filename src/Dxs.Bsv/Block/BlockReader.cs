using System;
using System.Collections.Generic;
using System.IO;

using Dxs.Bsv.Models;
using Dxs.Bsv.Protocol;

namespace Dxs.Bsv.Block;

public class BlockReader : IDisposable
{
    private readonly BitcoinStreamReader _bitcoinStreamReader;
    private readonly Network _network;

    private BlockReader(Stream stream, Network network)
    {
        _bitcoinStreamReader = new(stream);
        _network = network;
        ReadHeader();
    }

    public uint Version { get; set; }
    public byte[] PrevBlockHash { get; set; }
    public byte[] MerkleRoot { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public string Bits { get; set; }
    public uint Nonce { get; set; }
    public ulong TransactionCount { get; set; }

    private void ReadHeader()
    {
        Version = _bitcoinStreamReader.ReadUInt32Le();
        PrevBlockHash = _bitcoinStreamReader.ReadNBytes(32).Reverse();
        MerkleRoot = _bitcoinStreamReader.ReadNBytes(32);
        Timestamp = DateTimeOffset.FromUnixTimeSeconds(_bitcoinStreamReader.ReadUInt32Le());
        Bits = _bitcoinStreamReader.ReadNBytesLe(4).ToHexString();
        Nonce = _bitcoinStreamReader.ReadUInt32Le();
        TransactionCount = _bitcoinStreamReader.ReadVarInt();
    }

    public IEnumerable<Transaction> Transactions()
    {
        for (var i = 0; i < (int)TransactionCount; i++)
        {
            var transaction = Transaction.Parse(_bitcoinStreamReader, _network);

            yield return transaction;
        }
    }

    public static BlockReader Parse(string hex, Network network)
    {
        var bytes = hex.FromHexString();

        return Parse(bytes, network);
    }

    public static BlockReader Parse(byte[] bytes, Network network)
    {
        var stream = new MemoryStream(bytes);

        return Parse(stream, network);
    }

    public static BlockReader Parse(Stream stream, Network network)
    {
        var reader = new BlockReader(stream, network);

        return reader;
    }

    public void Dispose()
    {
        _bitcoinStreamReader?.Dispose();
    }
}
