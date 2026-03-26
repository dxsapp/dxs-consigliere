namespace Dxs.Consigliere.Data.Models.Tracking;

public abstract class TrackedEntityStatusDocumentBase : TrackedEntityDocumentBase
{
    public long? BackfillStartedAt { get; set; }
    public long? BackfillCompletedAt { get; set; }
    public long? RealtimeAttachedAt { get; set; }
    public long? GapClosedAt { get; set; }
    public int? HistoryAnchorBlockHeight { get; set; }
    public long? HistoryAnchorObservedAt { get; set; }
    public long? DegradedAt { get; set; }
    public bool? IntegritySafe { get; set; }
    public string FailureReason { get; set; }

    protected IEnumerable<string> StatusKeys()
    {
        yield return nameof(BackfillStartedAt);
        yield return nameof(BackfillCompletedAt);
        yield return nameof(RealtimeAttachedAt);
        yield return nameof(GapClosedAt);
        yield return nameof(HistoryAnchorBlockHeight);
        yield return nameof(HistoryAnchorObservedAt);
        yield return nameof(DegradedAt);
        yield return nameof(IntegritySafe);
        yield return nameof(FailureReason);
    }

    protected IEnumerable<string> StatusUpdateableKeys()
    {
        yield return nameof(BackfillStartedAt);
        yield return nameof(BackfillCompletedAt);
        yield return nameof(RealtimeAttachedAt);
        yield return nameof(GapClosedAt);
        yield return nameof(HistoryAnchorBlockHeight);
        yield return nameof(HistoryAnchorObservedAt);
        yield return nameof(DegradedAt);
        yield return nameof(IntegritySafe);
        yield return nameof(FailureReason);
    }

    protected IEnumerable<KeyValuePair<string, object>> StatusEntries()
    {
        yield return new KeyValuePair<string, object>(nameof(BackfillStartedAt), BackfillStartedAt);
        yield return new KeyValuePair<string, object>(nameof(BackfillCompletedAt), BackfillCompletedAt);
        yield return new KeyValuePair<string, object>(nameof(RealtimeAttachedAt), RealtimeAttachedAt);
        yield return new KeyValuePair<string, object>(nameof(GapClosedAt), GapClosedAt);
        yield return new KeyValuePair<string, object>(nameof(HistoryAnchorBlockHeight), HistoryAnchorBlockHeight);
        yield return new KeyValuePair<string, object>(nameof(HistoryAnchorObservedAt), HistoryAnchorObservedAt);
        yield return new KeyValuePair<string, object>(nameof(DegradedAt), DegradedAt);
        yield return new KeyValuePair<string, object>(nameof(IntegritySafe), IntegritySafe);
        yield return new KeyValuePair<string, object>(nameof(FailureReason), FailureReason);
    }
}
