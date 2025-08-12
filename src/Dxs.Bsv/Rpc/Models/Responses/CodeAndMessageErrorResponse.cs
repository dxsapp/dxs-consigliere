using System.Diagnostics;
using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

[DebuggerDisplay("Code: {Code}")]
public class CodeAndMessageErrorResponse
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("message")]
    public string Message { get; set; }
}