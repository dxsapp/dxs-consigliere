using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

public class RpcRawMemPoolResponse : RpcResponseBase<IDictionary<string, TxInfo>, CodeAndMessageErrorResponse>;

[DebuggerDisplay("Size: {Size}")]
public class TxInfo
{
    [JsonPropertyName("size")] public int Size { get; set; }

    [JsonPropertyName("fee")] public decimal Fee { get; set; }

    [JsonPropertyName("modifiedfee")] public decimal ModifiedFee { get; set; }

    [JsonPropertyName("time")] public int Time { get; set; }

    [JsonPropertyName("height")] public int Height { get; set; }

    [JsonPropertyName("depends")] public List<string> Depends { get; set; }
}