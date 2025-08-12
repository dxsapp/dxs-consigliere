using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class VoutDto
    {
        [JsonProperty("value")]
        public double Value { get; set; }

        [JsonProperty("n")]
        public int N { get; set; }

        [JsonProperty("scriptPubKey")]
        public ScriptPubKeyDto ScriptPubKey { get; set; }
    }
}