using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Journal;

public sealed class RavenObservationJournalReader(IDocumentStore documentStore)
{
    public async Task<IReadOnlyList<StoredObservationJournalRecord>> ReadAsync(
        JournalSequence afterSequence,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take), "Read size must be positive.");

        using var session = documentStore.GetSession();
        var query = session.Query<ObservationJournalRecordDocument>()
            .Where(x => x.Sequence > afterSequence.Value)
            .OrderBy(x => x.Sequence)
            .Take(take);

        var results = new List<StoredObservationJournalRecord>(take);
        await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);

        while (await stream.MoveNextAsync())
        {
            var document = stream.Current.Document;
            results.Add(new StoredObservationJournalRecord(
                new JournalSequence(document.Sequence),
                new DedupeFingerprint(document.Fingerprint),
                document.AppendedAt,
                document.ObservationType,
                document.ObservationJson,
                CreatePayloadReference(document)
            ));
        }

        return results;
    }

    internal static RawTransactionPayloadReference CreatePayloadReference(ObservationJournalRecordDocument document)
        => string.IsNullOrWhiteSpace(document.PayloadDocumentId)
            ? null
            : new RawTransactionPayloadReference(
                document.PayloadDocumentId,
                document.PayloadTxId,
                document.PayloadCompressionAlgorithm ?? RawTransactionPayloadCompressionAlgorithm.None
            );
}
