using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Dxs.Bsv.Tokens.Stas;

public class StasTokenSchema: ITokenSchema
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("tokenId")]
    public string TokenId { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; }

    [JsonPropertyName("satoshisPerToken")]
    public uint SatoshisPerToken { get; init; }

    [JsonPropertyName("decimals")]
    public uint Decimals { get; init; }

    public byte[] ToBytes() => Encoding.UTF8.GetBytes(ToJson());

    public string ToJson() => JsonSerializer.Serialize(this);
}