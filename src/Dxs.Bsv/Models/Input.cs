using Dxs.Bsv.Protocol;
using Dxs.Bsv.Script.Read;

namespace Dxs.Bsv.Models;

public class Input
{
    public string TxId { get; init; }

    public uint Vout { get; init; }

    public Slice ScriptSig { get; init; }

    public uint Sequence { get; set; }

    public bool Coinbase { get; init; }

    public Address Address { get; init; }

    public static Input Parse(BitcoinStreamReader bitcoinStreamReader, int txStartPosition, int length, string txId, uint vout, Network network)
    {
        var startPosition = bitcoinStreamReader.Position;
        var reader = UnlockingScriptReader.Read(bitcoinStreamReader, length, network);
        var sequence = bitcoinStreamReader.ReadUInt32Le();

        return new Input
        {
            TxId = txId,
            Vout = vout,
            ScriptSig = new()
            {
                Length = length,
                Start = startPosition - txStartPosition
            },
            Sequence = sequence,
            Coinbase = vout == uint.MaxValue,
            Address = reader.Address
        };
    }
}