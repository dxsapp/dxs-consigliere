using Dxs.Common.Extensions;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;

namespace Dxs.Consigliere.Data;

public class RavenRawTransactionPayloadStore(
    IDocumentStore documentStore
) : IRawTransactionPayloadStore
{
    public async Task<RawTransactionPayloadReference> SaveAsync(
        string txId,
        string payloadHex,
        string compressionAlgorithm = RawTransactionPayloadCompressionAlgorithm.None,
        CancellationToken cancellationToken = default
    )
    {
        if (txId.IsNullOrEmpty())
            throw new ArgumentException("Required", nameof(txId));

        if (payloadHex.IsNullOrEmpty())
            throw new ArgumentException("Required", nameof(payloadHex));

        if (!RawTransactionPayloadCompressionAlgorithm.IsSupported(compressionAlgorithm))
            throw new ArgumentOutOfRangeException(nameof(compressionAlgorithm), compressionAlgorithm, "Unsupported compression algorithm.");

        var id = RawTransactionPayloadDocument.GetId(txId);

        using var session = documentStore.GetSession();

        var existing = await session.LoadAsync<RawTransactionPayloadDocument>(id, cancellationToken);

        if (existing is not null)
        {
            if (existing.PayloadHex != payloadHex || existing.CompressionAlgorithm != compressionAlgorithm)
                throw new InvalidOperationException($"A different raw transaction payload is already stored for tx `{txId}`.");

            return ToReference(existing);
        }

        var document = new RawTransactionPayloadDocument
        {
            Id = id,
            TxId = txId,
            PayloadHex = payloadHex,
            CompressionAlgorithm = compressionAlgorithm,
            StoredAt = DateTimeOffset.UtcNow
        };

        await session.StoreAsync(document, id, cancellationToken);
        await session.SaveChangesAsync(cancellationToken);

        return ToReference(document);
    }

    public async Task<RawTransactionPayloadEnvelope> LoadByTxIdAsync(
        string txId,
        CancellationToken cancellationToken = default
    )
    {
        if (txId.IsNullOrEmpty())
            throw new ArgumentException("Required", nameof(txId));

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<RawTransactionPayloadDocument>(
            RawTransactionPayloadDocument.GetId(txId),
            cancellationToken
        );

        return document is null
            ? null
            : ToEnvelope(document);
    }

    public async Task<RawTransactionPayloadEnvelope> LoadAsync(
        RawTransactionPayloadReference reference,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(reference);

        using var session = documentStore.GetSession();
        var document = await session.LoadAsync<RawTransactionPayloadDocument>(reference.DocumentId, cancellationToken);

        return document is null
            ? null
            : ToEnvelope(document);
    }

    private static RawTransactionPayloadReference ToReference(RawTransactionPayloadDocument document)
        => new(document.Id, document.TxId, document.CompressionAlgorithm);

    private static RawTransactionPayloadEnvelope ToEnvelope(RawTransactionPayloadDocument document)
        => new(ToReference(document), document.PayloadHex);
}
