namespace Dxs.Common.Journal;

/// <summary>
/// Minimal progress marker for a projection over an observation journal.
/// </summary>
public readonly record struct ProjectionCheckpoint
{
    public JournalSequence Sequence { get; }

    public DedupeFingerprint? LastAppliedFingerprint { get; }

    public ProjectionCheckpoint(JournalSequence sequence, DedupeFingerprint? lastAppliedFingerprint = null)
    {
        Sequence = sequence;
        LastAppliedFingerprint = lastAppliedFingerprint;
    }

    public bool HasValue => Sequence.HasValue;

    public ProjectionCheckpoint AdvanceTo(JournalSequence sequence, DedupeFingerprint? lastAppliedFingerprint = null)
        => new(sequence, lastAppliedFingerprint);
}
