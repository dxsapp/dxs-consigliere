using System.Collections.Generic;

using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class ScriptPubKeyDto
    {
        [JsonProperty("asm")]
        public string Asm { get; set; }

        [JsonProperty("hex")]
        public string Hex { get; set; }

        [JsonProperty("reqSigs")]
        public int ReqSigs { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("addresses")]
        public List<string> Addresses { get; set; }

        [JsonProperty("isTruncated")]
        public bool IsTruncated { get; set; }
    }
}
