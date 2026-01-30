using System.Text.Json.Serialization;

namespace Dxs.Bsv.Zmq.Dto;

public struct DiscardedFromMempoolMessage
{
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }

    [JsonPropertyName("collidedWith")]
    public CollidedTx CollidedWith { get; set; }

    [JsonPropertyName("blockhash")]
    public string BlockHash { get; set; }
}
