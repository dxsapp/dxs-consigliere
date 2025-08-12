using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class ScriptSigDto
    {
        [JsonProperty("asm")]
        public string Asm { get; set; }

        [JsonProperty("hex")]
        public string Hex { get; set; }
    }
}