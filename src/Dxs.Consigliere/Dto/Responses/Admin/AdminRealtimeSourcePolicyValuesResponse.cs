namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminRealtimeSourcePolicyValuesResponse
{
    public string PrimaryRealtimeSource { get; set; }
    public string[] FallbackSources { get; set; } = [];
    public string BitailsTransport { get; set; }
}
