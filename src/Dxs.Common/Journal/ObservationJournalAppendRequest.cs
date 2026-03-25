namespace Dxs.Common.Journal;

/// <summary>
/// Append request for a single journal observation.
/// </summary>
public sealed class ObservationJournalAppendRequest<TObservation>
{
    public ObservationJournalAppendRequest(
        TObservation observation,
        DedupeFingerprint fingerprint,
        JournalSequence? expectedPreviousSequence = null)
    {
        Observation = observation;
        Fingerprint = fingerprint;
        ExpectedPreviousSequence = expectedPreviousSequence;
    }

    public TObservation Observation { get; }

    public DedupeFingerprint Fingerprint { get; }

    public JournalSequence? ExpectedPreviousSequence { get; }
}
