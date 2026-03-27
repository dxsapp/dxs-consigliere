namespace Dxs.Consigliere.Dto.Requests;

public sealed class AdminRealtimeSourcePolicyUpdateRequest
{
    public string PrimaryRealtimeSource { get; set; }
    public string BitailsTransport { get; set; }
}
