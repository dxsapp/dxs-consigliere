namespace Dxs.Consigliere.Data.Journal;

public sealed record ObservationJournalEntry<TObservation>(
    TObservation Observation,
    RawTransactionPayloadReference PayloadReference = null
);
