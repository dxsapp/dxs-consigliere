namespace Dxs.Common.Journal;

/// <summary>
/// Result of appending a single journal observation.
/// </summary>
public readonly record struct ObservationJournalAppendResult
{
    public ObservationJournalAppendResult(JournalSequence sequence, bool isDuplicate)
    {
        Sequence = sequence;
        IsDuplicate = isDuplicate;
    }

    public JournalSequence Sequence { get; }

    public bool IsDuplicate { get; }
}
