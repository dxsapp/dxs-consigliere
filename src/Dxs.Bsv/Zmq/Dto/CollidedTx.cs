using System.Text.Json.Serialization;

namespace Dxs.Bsv.Zmq.Dto;

public struct CollidedTx
{
    [JsonPropertyName("txid")]
    public string TxId { get; set; }

    [JsonPropertyName("size")]
    public ulong Size { get; set; }

    [JsonPropertyName("hex")]
    public string Hex { get; set; }
}