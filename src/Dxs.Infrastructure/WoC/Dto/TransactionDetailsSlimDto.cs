using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto;

public class TransactionDetailsSlimDto
{
    [JsonProperty("txid", Required = Required.Always)]
    public string Id { get; init; }

    [JsonProperty("hex")]
    public string Hex { get; init; }

    [JsonProperty("blockhash")]
    public string BlockHash { get; init; }

    [JsonProperty("blockheight")]
    public int BlockHeight { get; init; }

    [JsonProperty("blocktime")]
    public long BlockTime { get; init; }
}
