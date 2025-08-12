using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;

namespace Dxs.Consigliere.Data.Models.Transactions;

public class MetaOutput
{
    public string Id { get; init; }

    public string TxId { get; init; }
    public int Vout { get; set; }

    public int InputIdx { get; set; }
    public string SpendTxId { get; set; }


    public ScriptType Type { get; set; }
    public long Satoshis { get; set; }
    public string ScriptPubKey { get; set; }
    public string Address { get; set; }
    public string TokenId { get; set; }

    /// <summary>
    /// Hash160 of address to send (for redeem)
    /// </summary>
    public string Hash160 { get; set; }

    public string Symbol { get; set; }

    public bool Spent { get; set; }

    public static string GetId(string txId, int vout) => $"{txId}:{vout}";
    public static string GetId(string txId, uint vout) => GetId(txId, (int)vout);


    public static MetaOutput FromOutput(Transaction transaction, Output output, long timestamp, int height)
    {
        var outPoint = new OutPoint(transaction, output.Idx);
        var symbol = output.Type == ScriptType.P2STAS
            ? LockingScriptReader.Read(outPoint.ScriptPubKey, transaction.Network).GetSymbol()
            : null;
        var scriptPubKey = outPoint.ScriptPubKey.ToHexString();

        return new()
        {
            Id = GetId(transaction.Id, (int)output.Idx),

            TxId = transaction.Id,
            Vout = (int)output.Idx,

            InputIdx = 0,
            SpendTxId = null,

            Type = output.Type,
            Satoshis = (long)output.Satoshis,
            Address = output.Address?.Value,
            TokenId = output.TokenId,
            Hash160 = output.Address?.Hash160.ToHexString(),
            ScriptPubKey = scriptPubKey,
            Symbol = symbol,

            Spent = false
        };
    }

    public static MetaOutput FromInput(Transaction transaction, Input input, int idx, long timestamp, int height)
        => new()
        {
            Id = GetId(input.TxId, (int)input.Vout),

            TxId = input.TxId,
            Vout = (int)input.Vout,

            InputIdx = idx,
            SpendTxId = transaction.Id,

            Type = ScriptType.Unknown,
            Satoshis = 0,
            Address = null,
            TokenId = null,
            Hash160 = null,
            ScriptPubKey = null,
            Symbol = null,

            Spent = true,
        };
}