using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;

public class RpcGetBlockChainInfoResponse : RpcResponseBase<ResultObject, CodeAndMessageErrorResponse>;

public class ResultObject
{
    public class SoftFork
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("version")]
        public long Version { get; set; }

        [JsonPropertyName("reject")]
        public Reject Reject { get; set; }
    }

    public class Reject
    {
        [JsonPropertyName("status")]
        public bool Status { get; set; }
    }

    [JsonPropertyName("chain")]
    public string Chain { get; set; }

    [JsonPropertyName("blocks")]
    public long Blocks { get; set; }

    [JsonPropertyName("headers")]
    public long Headers { get; set; }

    [JsonPropertyName("bestblockhash")]
    public string BestBlockHash { get; set; }

    [JsonPropertyName("difficulty")]
    public double Difficulty { get; set; }

    [JsonPropertyName("mediantime")]
    public long MedianTime { get; set; }

    [JsonPropertyName("verificationprogress")]
    public double VerificationProgress { get; set; }

    [JsonPropertyName("chainwork")]
    public string ChainWork { get; set; }

    [JsonPropertyName("pruned")]
    public bool Pruned { get; set; }

    [JsonPropertyName("softforks")]
    public SoftFork[] SoftForks { get; set; }

    [JsonPropertyName("pruneheight")]
    public long PruneHeight { get; set; }
}
