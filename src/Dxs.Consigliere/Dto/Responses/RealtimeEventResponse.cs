namespace Dxs.Consigliere.Dto.Responses;

public class RealtimeEventResponse
{
    public string EventId { get; set; }
    public string EventType { get; set; }
    public string EntityType { get; set; }
    public string EntityId { get; set; }
    public string TxId { get; set; }
    public int? BlockHeight { get; set; }
    public DateTimeOffset Timestamp { get; set; }
    public bool Authoritative { get; set; }
    public string LifecycleStatus { get; set; }
    public Dictionary<string, object> Payload { get; set; } = [];
}
