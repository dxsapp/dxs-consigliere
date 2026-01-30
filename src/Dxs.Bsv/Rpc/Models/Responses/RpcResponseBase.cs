using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

public abstract class RpcResponseBase<TResult> : RpcResponseBase<TResult, string>;

public abstract class RpcResponseBase<TResult, TError>
{
    [JsonPropertyName("result")]
    public TResult Result { get; set; }

    [JsonPropertyName("id")]
    public string RequestId { get; set; }

    [JsonPropertyName("error")]
    public TError Error { get; set; }
}
