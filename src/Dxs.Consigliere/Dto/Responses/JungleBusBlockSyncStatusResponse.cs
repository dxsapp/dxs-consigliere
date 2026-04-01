namespace Dxs.Consigliere.Dto.Responses;

public sealed class JungleBusBlockSyncStatusResponse
{
    public bool Primary { get; set; }
    public bool Configured { get; set; }
    public bool Healthy { get; set; }
    public bool Degraded { get; set; }
    public string UnavailableReason { get; set; }
    public string BaseUrl { get; set; }
    public bool BlockSubscriptionIdConfigured { get; set; }
    public int? LastObservedBlockHeight { get; set; }
    public int? HighestKnownLocalBlockHeight { get; set; }
    public int? LagBlocks { get; set; }
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
    public string LastError { get; set; }
    public long? LastErrorAt { get; set; }
}
