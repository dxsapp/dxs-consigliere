using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace Dxs.Infrastructure.JungleBus.Dto;

public class PubTransactionDto
{
    [DataMember(Name = "id")]
    [JsonProperty(PropertyName = "id")]
    public string Id { get; set; }

    [DataMember(Name = "block_hash")]
    [JsonProperty(PropertyName = "block_hash")]
    public string BlockHash { get; set; }

    [DataMember(Name = "block_height")]
    [JsonProperty(PropertyName = "block_height")]
    public int BlockHeight { get; set; }

    [DataMember(Name = "block_index")]
    [JsonProperty(PropertyName = "block_index")]
    public int BlockIndex { get; set; }

    [DataMember(Name = "block_time")]
    [JsonProperty(PropertyName = "block_time")]
    public long BlockTime { get; set; }

    [DataMember(Name = "transaction")]
    [JsonProperty(PropertyName = "transaction")]
    public string TransactionBase64 { get; set; }

    [DataMember(Name = "merkle_proof")]
    [JsonProperty(PropertyName = "merkle_proof")]
    public string MerkleProof { get; set; }
}