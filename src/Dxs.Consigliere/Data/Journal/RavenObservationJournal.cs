using System.Text.Json;

using Dxs.Common.Journal;
using Dxs.Consigliere.Data.Models.Journal;
using Dxs.Consigliere.Extensions;

using Raven.Client.Documents;
using Raven.Client.Documents.Linq;

namespace Dxs.Consigliere.Data.Journal;

public sealed class RavenObservationJournal<TObservation>(
    IDocumentStore documentStore
) : IObservationJournalAppender<ObservationJournalEntry<TObservation>>
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public async ValueTask<ObservationJournalAppendResult> AppendAsync(
        ObservationJournalAppendRequest<ObservationJournalEntry<TObservation>> request,
        CancellationToken cancellationToken = default
    )
    {
        ArgumentNullException.ThrowIfNull(request);

        using var session = documentStore.GetClusterSession();

        var fingerprintId = ObservationJournalFingerprintDocument.GetId(request.Fingerprint);
        var existingFingerprint = await session.LoadAsync<ObservationJournalFingerprintDocument>(fingerprintId, cancellationToken);

        if (existingFingerprint is not null)
            return new ObservationJournalAppendResult(new JournalSequence(existingFingerprint.Sequence), true);

        var state = await session.LoadAsync<ObservationJournalSequenceState>(
            ObservationJournalSequenceState.DocumentId,
            cancellationToken
        );

        var previousSequence = state is null
            ? JournalSequence.Empty
            : new JournalSequence(state.LastAllocatedSequence);

        if (request.ExpectedPreviousSequence is JournalSequence expected && expected != previousSequence)
            throw new InvalidOperationException(
                $"Observation journal expected previous sequence `{expected}` but found `{previousSequence}`.");

        var nextSequence = previousSequence.Next();
        var payloadReference = request.Observation.PayloadReference;
        var record = new ObservationJournalRecordDocument
        {
            Id = ObservationJournalRecordDocument.GetId(nextSequence),
            Sequence = nextSequence.Value,
            Fingerprint = request.Fingerprint.Value,
            ObservationType = ObservationTypeIdentity.For<TObservation>(),
            ObservationJson = JsonSerializer.Serialize(request.Observation.Observation, SerializerOptions),
            PayloadDocumentId = payloadReference?.DocumentId,
            PayloadTxId = payloadReference?.TxId,
            PayloadCompressionAlgorithm = payloadReference?.CompressionAlgorithm,
            AppendedAt = DateTimeOffset.UtcNow
        };

        if (state is null)
        {
            state = new ObservationJournalSequenceState
            {
                Id = ObservationJournalSequenceState.DocumentId,
                LastAllocatedSequence = nextSequence.Value
            };

            await session.StoreAsync(state, state.Id, cancellationToken);
        }
        else
        {
            state.LastAllocatedSequence = nextSequence.Value;
        }

        await session.StoreAsync(record, record.Id, cancellationToken);
        await session.StoreAsync(
            new ObservationJournalFingerprintDocument
            {
                Id = fingerprintId,
                Sequence = nextSequence.Value,
                JournalRecordId = record.Id
            },
            fingerprintId,
            cancellationToken
        );

        await session.SaveChangesAsync(cancellationToken);

        return new ObservationJournalAppendResult(nextSequence, false);
    }

    public async Task<IReadOnlyList<StoredObservationJournalEntry<TObservation>>> ReadAsync(
        JournalSequence afterSequence,
        int take,
        CancellationToken cancellationToken = default
    )
    {
        if (take <= 0)
            throw new ArgumentOutOfRangeException(nameof(take), "Read size must be positive.");

        using var session = documentStore.GetSession();
        var observationType = ObservationTypeIdentity.For<TObservation>();
        var query = session.Query<ObservationJournalRecordDocument>()
            .Where(x => x.Sequence > afterSequence.Value)
            .Where(x => x.ObservationType == observationType)
            .OrderBy(x => x.Sequence)
            .Take(take);

        var results = new List<StoredObservationJournalEntry<TObservation>>(take);
        await using var stream = await session.Advanced.StreamAsync(query, cancellationToken);

        while (await stream.MoveNextAsync())
        {
            var document = stream.Current.Document;
            var observation = JsonSerializer.Deserialize<TObservation>(document.ObservationJson, SerializerOptions);

            if (observation is null)
                throw new InvalidOperationException($"Failed to deserialize journal observation `{document.Id}` as `{document.ObservationType}`.");

            results.Add(
                new StoredObservationJournalEntry<TObservation>(
                    new JournalSequence(document.Sequence),
                    new DedupeFingerprint(document.Fingerprint),
                    document.AppendedAt,
                    observation,
                    RavenObservationJournalReader.CreatePayloadReference(document)
                )
            );
        }

        return results;
    }
}
