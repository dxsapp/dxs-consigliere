namespace Dxs.Consigliere.Data;

public interface IRawTransactionPayloadStore
{
    Task<RawTransactionPayloadReference> SaveAsync(
        string txId,
        string payloadHex,
        string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
        CancellationToken cancellationToken = default
    );

    Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(
        string txId,
        CancellationToken cancellationToken = default
    );

    Task<RawTransactionPayloadEnvelope> LoadAsync(
        RawTransactionPayloadReference reference,
        CancellationToken cancellationToken = default
    );
}
