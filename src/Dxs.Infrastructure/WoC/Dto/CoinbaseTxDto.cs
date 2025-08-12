using System.Collections.Generic;
using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class CoinbaseTxDto
    {
        [JsonProperty("txid")]
        public string Txid { get; set; }

        [JsonProperty("hash")]
        public string Hash { get; set; }

        [JsonProperty("version")]
        public int Version { get; set; }

        [JsonProperty("size")]
        public int Size { get; set; }

        [JsonProperty("locktime")]
        public int LockTime { get; set; }

        [JsonProperty("vin")]
        public List<VinDto> Vin { get; set; }

        [JsonProperty("vout")]
        public List<VoutDto> Vout { get; set; }

        [JsonProperty("blockhash")]
        public string BlockHash { get; set; }

        [JsonProperty("confirmations")]
        public int Confirmations { get; set; }

        [JsonProperty("time")]
        public int Time { get; set; }

        [JsonProperty("blocktime")]
        public int BlockTime { get; set; }

        [JsonProperty("blockheight")]
        public int BlockHeight { get; set; }
    }

}