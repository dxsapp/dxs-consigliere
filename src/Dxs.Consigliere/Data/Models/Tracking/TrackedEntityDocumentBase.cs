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
        yield return new KeyValuePair<string, object>(nameof(IsTombstoned), IsTombstoned);
        yield return new KeyValuePair<string, object>(nameof(TombstonedAt), TombstonedAt);
    }
}
