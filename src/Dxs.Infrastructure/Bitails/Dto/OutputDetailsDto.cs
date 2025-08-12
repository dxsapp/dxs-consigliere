using Newtonsoft.Json;

namespace Dxs.Infrastructure.Bitails.Dto;

public class OutputDetailsDto
{
    public class OutputSpentDetailsDto
    {
        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("inputIndex")]
        public int InputIndex { get; set; }
    }

    [JsonProperty("index")]
    public int Vout { get; set; }

    [JsonProperty("type")]
    public string Type { get; set; }

    [JsonProperty("satoshis")]
    public long Satoshis { get; set; }

    [JsonProperty("spent")]
    public bool Spent { get; set; }

    [JsonProperty("spentIn")]
    public OutputSpentDetailsDto SpentDetails { get; set; }
}