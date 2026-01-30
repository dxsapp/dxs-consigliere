using System.Collections.Generic;
using System.IO;

using Dxs.Bsv.Protocol;
using Dxs.Bsv.Transactions.Read;

namespace Dxs.Bsv.Models;

public class Transaction(Network network)
{
    private readonly List<Input> _inputs = new();
    private readonly List<Output> _outputs = new();

    private string _id;
    private string _hex;

    public string Id
    {
        get => _id ??= BitcoinHelpers.GetTxId(Raw);
        set => _id = value;
    }
    public IList<Input> Inputs => _inputs;
    public IList<Output> Outputs => _outputs;

    public byte[] Raw { get; set; }
    public Network Network { get; init; } = network;

    public string Hex => _hex ??= Raw.ToHexString();

    public static Transaction Parse(string hex, Network network)
    {
        var bytes = hex.FromHexString();

        return Parse(bytes, network);
    }

    public static Transaction Parse(byte[] bytes, Network network)
    {
        using var stream = new MemoryStream(bytes);

        return Parse(stream, network);
    }

    public static Transaction Parse(Stream stream, Network network)
    {
        var reader = new TransactionReader(stream, network);
        return reader.Read();
    }

    public static Transaction Parse(BitcoinStreamReader streamReader, Network network)
    {
        var reader = new TransactionReader(streamReader, network);
        return reader.Read();
    }

    public static bool TryParse<T>(T data, Network network, out Transaction transaction)
    {
        transaction = null;

        try
        {
            if (data is string str)
                transaction = Parse(str, network);
            else if (data is byte[] bytes)
                transaction = Parse(bytes, network);
            else if (data is Stream stream)
                transaction = Parse(stream, network);
            else if (data is BitcoinStreamReader bitcoinStreamReader)
                transaction = Parse(bitcoinStreamReader, network);
            else
                return false;

            return true;
        }
        catch
        {
            return false;
        }
    }

    public override string ToString() => Id;
}
