using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

public class ChainTipsResponse : RpcResponseBase<IList<ChainTipsResponse.ChainTip>, CodeAndMessageErrorResponse>
{
    public class ChainTip
    {
        [JsonPropertyName("height")]
        public int Height { get; set; }
            
        [JsonPropertyName("hash")]
        public string Hash { get; set; }
            
        [JsonPropertyName("branchlen")]
        public int BranchLength { get; set; }
            
        [JsonPropertyName("status")]
        public string Status { get; set; }
    }
}