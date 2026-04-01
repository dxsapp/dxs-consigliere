namespace Dxs.Consigliere.Dto.Responses;

public sealed class JungleBusChainTipAssuranceResponse
{
    public bool Primary { get; set; }
    public bool Configured { get; set; }
    public string State { get; set; }
    public string AssuranceMode { get; set; }
    public bool SingleSourceAssurance { get; set; }
    public bool SecondaryCrossCheckAvailable { get; set; }
    public bool ControlFlowStalled { get; set; }
    public bool LocalProgressStalled { get; set; }
    public string UnavailableReason { get; set; }
    public string Note { get; set; }
    public int? LastObservedBlockHeight { get; set; }
    public int? HighestKnownLocalBlockHeight { get; set; }
    public int? LagBlocks { get; set; }
    public long? LastObservedMovementAt { get; set; }
    public int? LastObservedMovementHeight { get; set; }
    public long? LastLocalProgressAt { get; set; }
    public int? LastLocalProgressHeight { get; set; }
    public long? LastControlMessageAt { get; set; }
    public long? LastScheduledAt { get; set; }
    public long? LastProcessedAt { get; set; }
    public string LastError { get; set; }
    public long? LastErrorAt { get; set; }
    public int ControlFlowStaleAfterSeconds { get; set; }
    public int LocalProgressStaleAfterSeconds { get; set; }
}
