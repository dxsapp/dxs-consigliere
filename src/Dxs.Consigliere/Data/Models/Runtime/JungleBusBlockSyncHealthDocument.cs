using Dxs.Consigliere.Data.Models;

namespace Dxs.Consigliere.Data.Models.Runtime;

public sealed class JungleBusBlockSyncHealthDocument : AuditableEntity
{
    public const string DocumentId = "operator/runtime/junglebus-block-sync-health";

    public string SubscriptionId { get; set; }
    public int? LastObservedBlockHeight { get; set; }
    public long? LastObservedBlockTimestamp { get; set; }
    public long? LastControlMessageAt { get; set; }
    public int? LastControlCode { get; set; }
    public string LastControlStatus { get; set; }
    public string LastControlMessage { get; set; }
    public long? LastScheduledAt { get; set; }
    public int? LastScheduledFromHeight { get; set; }
    public int? LastScheduledToHeight { get; set; }
    public long? LastProcessedAt { get; set; }
    public int? LastProcessedBlockHeight { get; set; }
    public string LastRequestId { get; set; }
    public long? LastErrorAt { get; set; }
    public string LastError { get; set; }

    public override string GetId() => DocumentId;

    public override IEnumerable<string> AllKeys()
    {
        foreach (var key in base.AllKeys())
            yield return key;

        yield return nameof(SubscriptionId);
        yield return nameof(LastObservedBlockHeight);
        yield return nameof(LastObservedBlockTimestamp);
        yield return nameof(LastControlMessageAt);
        yield return nameof(LastControlCode);
        yield return nameof(LastControlStatus);
        yield return nameof(LastControlMessage);
        yield return nameof(LastScheduledAt);
        yield return nameof(LastScheduledFromHeight);
        yield return nameof(LastScheduledToHeight);
        yield return nameof(LastProcessedAt);
        yield return nameof(LastProcessedBlockHeight);
        yield return nameof(LastRequestId);
        yield return nameof(LastErrorAt);
        yield return nameof(LastError);
    }

    public override IEnumerable<string> UpdateableKeys()
    {
        foreach (var key in base.UpdateableKeys())
            yield return key;

        yield return nameof(SubscriptionId);
        yield return nameof(LastObservedBlockHeight);
        yield return nameof(LastObservedBlockTimestamp);
        yield return nameof(LastControlMessageAt);
        yield return nameof(LastControlCode);
        yield return nameof(LastControlStatus);
        yield return nameof(LastControlMessage);
        yield return nameof(LastScheduledAt);
        yield return nameof(LastScheduledFromHeight);
        yield return nameof(LastScheduledToHeight);
        yield return nameof(LastProcessedAt);
        yield return nameof(LastProcessedBlockHeight);
        yield return nameof(LastRequestId);
        yield return nameof(LastErrorAt);
        yield return nameof(LastError);
    }

    public override IEnumerable<KeyValuePair<string, object>> ToEntries()
    {
        foreach (var entry in base.ToEntries())
            yield return entry;

        yield return new KeyValuePair<string, object>(nameof(SubscriptionId), SubscriptionId);
        yield return new KeyValuePair<string, object>(nameof(LastObservedBlockHeight), LastObservedBlockHeight);
        yield return new KeyValuePair<string, object>(nameof(LastObservedBlockTimestamp), LastObservedBlockTimestamp);
        yield return new KeyValuePair<string, object>(nameof(LastControlMessageAt), LastControlMessageAt);
        yield return new KeyValuePair<string, object>(nameof(LastControlCode), LastControlCode);
        yield return new KeyValuePair<string, object>(nameof(LastControlStatus), LastControlStatus);
        yield return new KeyValuePair<string, object>(nameof(LastControlMessage), LastControlMessage);
        yield return new KeyValuePair<string, object>(nameof(LastScheduledAt), LastScheduledAt);
        yield return new KeyValuePair<string, object>(nameof(LastScheduledFromHeight), LastScheduledFromHeight);
        yield return new KeyValuePair<string, object>(nameof(LastScheduledToHeight), LastScheduledToHeight);
        yield return new KeyValuePair<string, object>(nameof(LastProcessedAt), LastProcessedAt);
        yield return new KeyValuePair<string, object>(nameof(LastProcessedBlockHeight), LastProcessedBlockHeight);
        yield return new KeyValuePair<string, object>(nameof(LastRequestId), LastRequestId);
        yield return new KeyValuePair<string, object>(nameof(LastErrorAt), LastErrorAt);
        yield return new KeyValuePair<string, object>(nameof(LastError), LastError);
    }
}
