using Dxs.Infrastructure.Common;
using Newtonsoft.Json;

namespace Dxs.Infrastructure.WoC.Dto
{
    public class UnspentOutputDto
    {
        [JsonProperty("tx_hash", Required = Required.Always)]
        public string TransactionId { get; init; }

        [JsonProperty("tx_pos", Required = Required.Always)]
        public uint OutputIndex { get; init; }

        [JsonProperty("value", Required = Required.Always)]
        public ulong AmountSatoshis { get; init; }

        public decimal Amount => AmountSatoshis / (decimal)CommonConstants.SatoshisInBsv;
    }
}