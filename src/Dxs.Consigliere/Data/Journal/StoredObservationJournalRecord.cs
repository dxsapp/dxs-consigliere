using System.Text.Json;

using Dxs.Common.Journal;

namespace Dxs.Consigliere.Data.Journal;

public sealed record StoredObservationJournalRecord(
    JournalSequence Sequence,
    DedupeFingerprint Fingerprint,
    DateTimeOffset AppendedAt,
    string ObservationType,
    string ObservationJson,
    RawTransactionPayloadReference PayloadReference = null
)
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    public bool IsObservationType<TObservation>()
        => string.Equals(ObservationType, ObservationTypeIdentity.For<TObservation>(), StringComparison.Ordinal);

    public TObservation Deserialize<TObservation>()
    {
        var observation = JsonSerializer.Deserialize<TObservation>(ObservationJson, SerializerOptions);
        if (observation is null)
            throw new InvalidOperationException($"Failed to deserialize journal sequence `{Sequence.Value}` as `{ObservationTypeIdentity.For<TObservation>()}`.");

        return observation;
    }
}
