using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Tracking;

public abstract class TrackedEntityDocumentBase : AuditableEntity
{
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public bool Tracked { get; set; } = true;
    public string LifecycleStatus { get; set; } = TrackedEntityLifecycleStatus.Registered;
    public bool Readable { get; set; }
    public bool Authoritative { get; set; }
    public bool Degraded { get; set; }
    public int? LagBlocks { get; set; }
    public double? Progress { get; set; }
    public string HistoryMode { get; set; } = TrackedEntityHistoryMode.ForwardOnly;
    public string HistoryReadiness { get; set; } = TrackedEntityHistoryReadiness.NotRequested;
    public TrackedHistoryCoverage HistoryCoverage { get; set; } = new();
    public string HistoryBackfillStatus { get; set; }
    public long? HistoryBackfillRequestedAt { get; set; }
    public long? HistoryBackfillStartedAt { get; set; }
    public long? HistoryBackfillLastProgressAt { get; set; }
    public long? HistoryBackfillCompletedAt { get; set; }
    public int HistoryBackfillItemsScanned { get; set; }
    public int HistoryBackfillItemsApplied { get; set; }
    public string HistoryBackfillErrorCode { get; set; }
    public bool IsTombstoned { get; set; }
    public long? TombstonedAt { get; set; }

    protected IEnumerable<string> TrackedKeys()
    {
        yield return nameof(EntityType);
        yield return nameof(EntityId);
        yield return nameof(Tracked);
        yield return nameof(LifecycleStatus);
        yield return nameof(Readable);
        yield return nameof(Authoritative);
        yield return nameof(Degraded);
        yield return nameof(LagBlocks);
        yield return nameof(Progress);
        yield return nameof(HistoryMode);
        yield return nameof(HistoryReadiness);
        yield return nameof(HistoryCoverage);
        yield return nameof(HistoryBackfillStatus);
        yield return nameof(HistoryBackfillRequestedAt);
        yield return nameof(HistoryBackfillStartedAt);
        yield return nameof(HistoryBackfillLastProgressAt);
        yield return nameof(HistoryBackfillCompletedAt);
        yield return nameof(HistoryBackfillItemsScanned);
        yield return nameof(HistoryBackfillItemsApplied);
        yield return nameof(HistoryBackfillErrorCode);
        yield return nameof(IsTombstoned);
        yield return nameof(TombstonedAt);
    }

    protected IEnumerable<string> TrackedUpdateableKeys()
    {
        yield return nameof(Tracked);
        yield return nameof(LifecycleStatus);
        yield return nameof(Readable);
        yield return nameof(Authoritative);
        yield return nameof(Degraded);
        yield return nameof(LagBlocks);
        yield return nameof(Progress);
        yield return nameof(HistoryMode);
        yield return nameof(HistoryReadiness);
        yield return nameof(HistoryCoverage);
        yield return nameof(HistoryBackfillStatus);
        yield return nameof(HistoryBackfillRequestedAt);
        yield return nameof(HistoryBackfillStartedAt);
        yield return nameof(HistoryBackfillLastProgressAt);
        yield return nameof(HistoryBackfillCompletedAt);
        yield return nameof(HistoryBackfillItemsScanned);
        yield return nameof(HistoryBackfillItemsApplied);
        yield return nameof(HistoryBackfillErrorCode);
        yield return nameof(IsTombstoned);
        yield return nameof(TombstonedAt);
    }

    protected IEnumerable<KeyValuePair<string, object>> TrackedEntries()
    {
        yield return new KeyValuePair<string, object>(nameof(EntityType), EntityType);
        yield return new KeyValuePair<string, object>(nameof(EntityId), EntityId);
        yield return new KeyValuePair<string, object>(nameof(Tracked), Tracked);
        yield return new KeyValuePair<string, object>(nameof(LifecycleStatus), LifecycleStatus);
        yield return new KeyValuePair<string, object>(nameof(Readable), Readable);
        yield return new KeyValuePair<string, object>(nameof(Authoritative), Authoritative);
        yield return new KeyValuePair<string, object>(nameof(Degraded), Degraded);
        yield return new KeyValuePair<string, object>(nameof(LagBlocks), LagBlocks);
        yield return new KeyValuePair<string, object>(nameof(Progress), Progress);
        yield return new KeyValuePair<string, object>(nameof(HistoryMode), HistoryMode);
        yield return new KeyValuePair<string, object>(nameof(HistoryReadiness), HistoryReadiness);
        yield return new KeyValuePair<string, object>(nameof(HistoryCoverage), HistoryCoverage);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillStatus), HistoryBackfillStatus);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillRequestedAt), HistoryBackfillRequestedAt);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillStartedAt), HistoryBackfillStartedAt);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillLastProgressAt), HistoryBackfillLastProgressAt);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillCompletedAt), HistoryBackfillCompletedAt);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillItemsScanned), HistoryBackfillItemsScanned);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillItemsApplied), HistoryBackfillItemsApplied);
        yield return new KeyValuePair<string, object>(nameof(HistoryBackfillErrorCode), HistoryBackfillErrorCode);
        yield return new KeyValuePair<string, object>(nameof(IsTombstoned), IsTombstoned);
        yield return new KeyValuePair<string, object>(nameof(TombstonedAt), TombstonedAt);
    }
}
