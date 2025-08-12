using System.Text.Json.Serialization;

namespace Dxs.Bsv.Rpc.Models.Responses;
    
public class RpcGetBlockHeaderResponse : RpcResponseBase<RpcGetBlockHeader, CodeAndMessageErrorResponse>;

public class RpcGetBlockHeader
{
    public class BlockStatus
    {
        /// <summary>
        /// Validation state of the block
        /// </summary>
        [JsonPropertyName("validity")]
        public string Validity { get; set; }

        [JsonPropertyName("data")]
        public bool Data { get; set; }

        [JsonPropertyName("undo")]
        public bool Undo { get; set; }

        [JsonPropertyName("failed")]
        public bool Failed { get; set; }

        [JsonPropertyName("parent failed")]
        public bool ParentFailed { get; set; }

        [JsonPropertyName("disk meta")]
        public bool DiskMeta { get; set; }

        [JsonPropertyName("soft reject")]
        public bool SoftReject { get; set; }

        /// <summary>
        /// May contain a double spend tx
        /// </summary>
        [JsonPropertyName("double spend")]
        public bool DoubleSpend { get; set; }

        [JsonPropertyName("soft consensus frozen")]
        public bool SoftConsensusFrozen { get; set; }
    }

    [JsonPropertyName("hash")]
    public string Hash { get; set; }

    /// <summary>
    /// The number of confirmations, or -1 if the block is not on the main chain
    /// </summary>
    [JsonPropertyName("confirmations")]
    public int Confirmations { get; set; }

    [JsonPropertyName("height")]
    public int Height { get; set; }

    [JsonPropertyName("version")]
    public int Version { get; set; }

    [JsonPropertyName("versionHex")]
    public string VersionHex { get; set; }

    [JsonPropertyName("merkleroot")]
    public string MerkleRoot { get; set; }

    [JsonPropertyName("num_tx")]
    public int TransactionsCount { get; set; }

    /// <summary>
    /// The block time in seconds since epoch (Jan 1 1970 GMT)
    /// </summary>
    [JsonPropertyName("time")]
    public long Time { get; set; }

    /// <summary>
    /// The median block time in seconds since epoch (Jan 1 1970 GMT)
    /// </summary>
    [JsonPropertyName("mediantime")]
    public long MedianTime { get; set; }

    [JsonPropertyName("nonce")]
    public long Nonce { get; set; }

    [JsonPropertyName("bits")]
    public string Bits { get; set; }

    [JsonPropertyName("difficulty")]
    public decimal Difficulty { get; set; }

    /// <summary>
    ///  Expected number of hashes required to produce the current chain (in hex)
    /// </summary>
    [JsonPropertyName("chainwork")]
    public string ChainWork { get; set; }

    [JsonPropertyName("previousblockhash")]
    public string PreviousBlockHash { get; set; }

    [JsonPropertyName("nextblockhash")]
    public string NextBlockHash { get; set; }

    [JsonPropertyName("status")]
    public BlockStatus Status { get; set; }
}