using Newtonsoft.Json;

namespace Dxs.Infrastructure.Bitails.Dto;

// https://docs.bitails.io/#get-transaction-by-id
public class TransactionDetailsDto
{
    [JsonProperty("txid")]
    public string TxId { get; set; }

    [JsonProperty("blockhash")]
    public string BlockHash { get; set; }

    [JsonProperty("blockheight")]
    public int BlockHeight { get; set; }

    [JsonProperty("confirmations")]
    public int Confirmations { get; set; }

    [JsonProperty("time")]
    public long Timestamp { get; set; }

    [JsonProperty("inblockIndex")]
    public int Idx { get; set; }

    [JsonProperty("partialOutputs")]
    public bool PartialOutputs { get; set; }

    [JsonProperty("outputs")]
    public OutputDetailsDto[] Outputs { get; set; }
}