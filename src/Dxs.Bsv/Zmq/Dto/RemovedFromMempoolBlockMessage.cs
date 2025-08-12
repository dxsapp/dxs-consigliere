using System.Text.Json.Serialization;

namespace Dxs.Bsv.Zmq.Dto;

public struct RemovedFromMempoolBlockMessage
{
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("reason")]
    public string Reason { get; set; }
}