using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models;

public class RpcRequest
{
    [JsonPropertyName("jsonrpc")]
    public string Version => "1.0";

    [JsonPropertyName("id")]
    public string RequestId => MethodName;

    [JsonPropertyName("method")]
    public string MethodName { get; set; }

    [JsonPropertyName("params")]
    public object[] Params { get; set; }
}