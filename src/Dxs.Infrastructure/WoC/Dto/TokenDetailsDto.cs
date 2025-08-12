using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class TokenDetailsDto
    {
        public class Payload
        {
            [JsonProperty("issuance_txs")]
            public string[] IssuanceTxs { get; set; }
        }

        [JsonProperty("token")]
        public Payload Token { get; set; }
    }
}