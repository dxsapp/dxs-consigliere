using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto;

public class TransactionDetailsDto
{
    [JsonProperty("txid", Required = Required.Always)]
    public string Id { get; set; }

    [JsonProperty("blockhash")]
    public string BlockHash { get; set; }

    [JsonProperty("blocktime")]
    public long Timestamp { get; set; }

    [JsonProperty("confirmations")]
    public int Confirmations { get; set; }

    public int Idx { get; set; }
}
