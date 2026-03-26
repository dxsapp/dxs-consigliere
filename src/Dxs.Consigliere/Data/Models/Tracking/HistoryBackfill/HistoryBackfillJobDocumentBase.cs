using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public abstract class HistoryBackfillJobDocumentBase : AuditableEntity
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string HistoryMode { get; set; } = TrackedEntityHistoryMode.FullHistory;
    public string Status { get; set; } = HistoryBackfillExecutionStatus.Queued;
    public long RequestedAt { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    public long? StartedAt { get; set; }
    public long? LastProgressAt { get; set; }
    public long? CompletedAt { get; set; }
    public string SourceCapability { get; set; }
    public string Provider { get; set; }
    public string Cursor { get; set; }
    public int ItemsScanned { get; set; }
    public int ItemsApplied { get; set; }
    public int? LastObservedHistoricalBlockHeight { get; set; }
    public string ErrorCode { get; set; }
    public int AttemptCount { get; set; }

    protected IEnumerable<string> JobKeys()
    {
        yield return nameof(EntityType);
        yield return nameof(EntityId);
        yield return nameof(HistoryMode);
        yield return nameof(Status);
        yield return nameof(RequestedAt);
        yield return nameof(StartedAt);
        yield return nameof(LastProgressAt);
        yield return nameof(CompletedAt);
        yield return nameof(SourceCapability);
        yield return nameof(Provider);
        yield return nameof(Cursor);
        yield return nameof(ItemsScanned);
        yield return nameof(ItemsApplied);
        yield return nameof(LastObservedHistoricalBlockHeight);
        yield return nameof(ErrorCode);
        yield return nameof(AttemptCount);
    }

    protected IEnumerable<KeyValuePair<string, object>> JobEntries()
    {
        yield return new(nameof(EntityType), EntityType);
        yield return new(nameof(EntityId), EntityId);
        yield return new(nameof(HistoryMode), HistoryMode);
        yield return new(nameof(Status), Status);
        yield return new(nameof(RequestedAt), RequestedAt);
        yield return new(nameof(StartedAt), StartedAt);
        yield return new(nameof(LastProgressAt), LastProgressAt);
        yield return new(nameof(CompletedAt), CompletedAt);
        yield return new(nameof(SourceCapability), SourceCapability);
        yield return new(nameof(Provider), Provider);
        yield return new(nameof(Cursor), Cursor);
        yield return new(nameof(ItemsScanned), ItemsScanned);
        yield return new(nameof(ItemsApplied), ItemsApplied);
        yield return new(nameof(LastObservedHistoricalBlockHeight), LastObservedHistoricalBlockHeight);
        yield return new(nameof(ErrorCode), ErrorCode);
        yield return new(nameof(AttemptCount), AttemptCount);
    }
}
