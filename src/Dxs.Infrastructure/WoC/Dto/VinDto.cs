using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class VinDto
    {
        [JsonProperty("coinbase")]
        public string Coinbase { get; set; }

        [JsonProperty("txid")]
        public string TxId { get; set; }

        [JsonProperty("vout")]
        public int Vout { get; set; }

        [JsonProperty("scriptSig")]
        public ScriptSigDto ScriptSig { get; set; }

        [JsonProperty("sequence")]
        public long Sequence { get; set; }
    }
}