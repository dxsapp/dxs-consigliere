namespace Dxs.Consigliere.Data.Models.Journal;

public class ObservationJournalSequenceState
{
    public const string DocumentId = "journal/state";

    public string Id { get; set; } = DocumentId;
    public long LastAllocatedSequence { get; set; }
}
