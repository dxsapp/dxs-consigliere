using Dxs.Common.Journal;

namespace Dxs.Consigliere.Data.Journal;

public sealed record StoredObservationJournalEntry<TObservation>(
    JournalSequence Sequence,
    DedupeFingerprint Fingerprint,
    DateTimeOffset AppendedAt,
    TObservation Observation,
    RawTransactionPayloadReference PayloadReference = null
);
