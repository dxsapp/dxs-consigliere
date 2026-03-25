namespace Dxs.Consigliere.Dto.Responses;

public class TransactionStateResponse
{
    public string TxId { get; set; }
    public bool Known { get; set; }
    public string LifecycleStatus { get; set; }
    public bool Authoritative { get; set; }
    public bool RelevantToManagedScope { get; set; }
    public string[] RelevanceTypes { get; set; } = [];
    public string[] SeenBySources { get; set; } = [];
    public bool? SeenInMempool { get; set; }
    public string BlockHash { get; set; }
    public int? BlockHeight { get; set; }
    public DateTimeOffset? FirstSeenAt { get; set; }
    public DateTimeOffset? LastObservedAt { get; set; }
    public string ValidationStatus { get; set; }
    public bool PayloadAvailable { get; set; }
}
