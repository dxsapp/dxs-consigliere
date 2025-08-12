using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dxs.Bsv.Tokens;

namespace Dxs.Consigliere.Configs;

public class TokenSchema: ITokenSchema
{

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("tokenId")]
    public string TokenId { get; init; }

    [JsonPropertyName("symbol")]
    public string Symbol { get; init; }

    [JsonPropertyName("satoshisPerToken")]
    public uint SatoshisPerToken { get; init; }

    [JsonPropertyName("terms")]
    public string Terms { get; init; }

    public byte[] ToBytes() => Encoding.UTF8.GetBytes(ToJson());

    public string ToJson() => JsonSerializer.Serialize(this);

    public decimal SatoshisToToken(long satoshis) => satoshis / (decimal)SatoshisPerToken;
}