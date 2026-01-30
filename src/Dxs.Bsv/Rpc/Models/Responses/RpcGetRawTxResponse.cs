using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

public class RpcGetRawTxResponse : RpcResponseBase<ResultJsonObject, CodeAndMessageErrorResponse>;

public class ResultJsonObject
{
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    [JsonPropertyName("version")]
    public long Version { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("locktime")]
    public long LockTime { get; set; }

    [JsonPropertyName("vin")]
    public Vin[] Vin { get; set; }

    [JsonPropertyName("vout")]
    public Vout[] Vout { get; set; }

    [JsonPropertyName("blockhash")]
    public string BlockHash { get; set; }

    [JsonPropertyName("confirmations")]
    public long Confirmations { get; set; }

    [JsonPropertyName("time")]
    public long Time { get; set; }

    [JsonPropertyName("blocktime")]
    public long BlockTime { get; set; }

    [JsonPropertyName("blockheight")]
    public long BlockHeight { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
}

public class Vin
{
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("vout")]
    public long Vout { get; set; }

    [JsonPropertyName("scriptSig")]
    public ScriptSig ScriptSig { get; set; }

    [JsonPropertyName("sequence")]
    public long Sequence { get; set; }
}

public class ScriptSig
{
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
}

public class Vout
{
    [JsonPropertyName("value")]
    public double Value { get; set; }

    [JsonPropertyName("n")]
    public long N { get; set; }

    [JsonPropertyName("scriptPubKey")]
    public ScriptPubKey ScriptPubKey { get; set; }
}

public class ScriptPubKey
{
    [JsonPropertyName("asm")]
    public string Asm { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("reqSigs")]
    public long? ReqSigs { get; set; }

    [JsonPropertyName("addresses")]
    public string[] Addresses { get; set; }
}
