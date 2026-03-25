namespace Dxs.Consigliere.Data.Models.Transactions;

public class RawTransactionPayloadDocument
{
    public string Id { get; set; } = string.Empty;
    public string TxId { get; set; } = string.Empty;
    public string PayloadHex { get; set; } = string.Empty;
    public string CompressionAlgorithm { get; set; } = RawTransactionPayloadCompressionAlgorithm.None;
    public DateTimeOffset StoredAt { get; set; }

    public static string GetId(string txId) => $"tx/payload/{txId}";
}
