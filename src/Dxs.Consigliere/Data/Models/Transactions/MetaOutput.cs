using Dxs.Bsv;
using Dxs.Bsv.Models;
using Dxs.Bsv.Script;
using Dxs.Bsv.Script.Read;
using Dxs.Bsv.Tokens.Dstas.Parsing;

using Dxs.Consigliere.Data.Transactions.Dstas;

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

    public string DstasFlags { get; set; }
    public bool? DstasFreezeEnabled { get; set; }
    public bool? DstasConfiscationEnabled { get; set; }
    public bool? DstasFrozen { get; set; }
    public string DstasFreezeAuthority { get; set; }
    public string DstasConfiscationAuthority { get; set; }
    public string[] DstasServiceFields { get; set; }
    public string DstasActionType { get; set; }
    public string DstasActionData { get; set; }
    public string DstasRequestedScriptHash { get; set; }
    public string[] DstasOptionalData { get; set; }
    public string DstasOptionalDataFingerprint { get; set; }

    public bool Spent { get; set; }

    public static string GetId(string txId, int vout) => $"{txId}:{vout}";
    public static string GetId(string txId, uint vout) => GetId(txId, (int)vout);


    public static MetaOutput FromOutput(Transaction transaction, Output output, long timestamp, int height)
    {
        var outPoint = new OutPoint(transaction, output.Idx);
        var reader = output.Type is ScriptType.P2STAS or ScriptType.DSTAS
            ? LockingScriptReader.Read(outPoint.ScriptPubKey, transaction.Network)
            : null;
        var symbol = output.Type == ScriptType.P2STAS
            ? reader?.GetSymbol()
            : null;
        var scriptPubKey = outPoint.ScriptPubKey.ToHexString();
        var dstas = DstasLockingScriptParser.Parse(reader);
        var dstasMapping = DstasMetaOutputMapping.FromSemantics(dstas);

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

            DstasFlags = dstasMapping.Flags,
            DstasFreezeEnabled = dstasMapping.FreezeEnabled,
            DstasConfiscationEnabled = dstasMapping.ConfiscationEnabled,
            DstasFrozen = dstasMapping.Frozen,
            DstasFreezeAuthority = dstasMapping.FreezeAuthority,
            DstasConfiscationAuthority = dstasMapping.ConfiscationAuthority,
            DstasServiceFields = dstasMapping.ServiceFields,
            DstasActionType = dstasMapping.ActionType,
            DstasActionData = dstasMapping.ActionData,
            DstasRequestedScriptHash = dstasMapping.RequestedScriptHash,
            DstasOptionalData = dstasMapping.OptionalData,
            DstasOptionalDataFingerprint = dstasMapping.OptionalDataFingerprint,

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

            DstasFlags = null,
            DstasFreezeEnabled = null,
            DstasConfiscationEnabled = null,
            DstasFrozen = null,
            DstasFreezeAuthority = null,
            DstasConfiscationAuthority = null,
            DstasServiceFields = null,
            DstasActionType = null,
            DstasActionData = null,
            DstasRequestedScriptHash = null,
            DstasOptionalData = null,
            DstasOptionalDataFingerprint = null,

            Spent = true,
        };
}
