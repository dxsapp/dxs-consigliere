namespace Dxs.Consigliere.Data;

public sealed record RawTransactionPayloadReference(
    string DocumentId,
    string TxId,
    string CompressionAlgorithm
);
