using Newtonsoft.Json;

namespace Dxs.Infrastructure.Bitails.Dto;

public class AddressDetailsDto
{
    public class BalanceDto
    {
        [JsonProperty("address")]
        public string Address { get; set; }

        [JsonProperty("scripthash")]
        public string ScriptHash { get; set; }

        [JsonProperty("confirmed")]
        public long ConfirmedSatoshis { get; set; }

        [JsonProperty("unconfirmed")]
        public long UnconfirmedSatoshis { get; set; }

        [JsonProperty("summary")]
        public long Satoshis { get; set; }

        [JsonProperty("count")]
        public int Count { get; set; }
    }

    public class HistoryRecord
    {
        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("inputSatoshis")]
        public long InputSatoshis { get; set; }

        [JsonProperty("outputSatoshis")]
        public long OutputSatoshis { get; set; }

        [JsonProperty("time")]
        public int Timestamp { get; set; }
    }

    [JsonProperty("balance")]
    public BalanceDto Balance { get; set; }

    [JsonProperty("firstSeen")]
    public HistoryRecord FirstSeen { get; set; }

    [JsonProperty("lastSeen")]
    public HistoryRecord LastSeen { get; set; }
}
