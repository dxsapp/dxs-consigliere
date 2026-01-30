using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto;

public class SpentTransactionOutput
{
    [JsonProperty("txid", Required = Required.Always)]
    public string TransactionId { get; set; }


    [JsonProperty("vin")]
    public ulong Vin { get; set; }
}
