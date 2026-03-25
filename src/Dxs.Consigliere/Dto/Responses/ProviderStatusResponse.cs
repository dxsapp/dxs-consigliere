namespace Dxs.Consigliere.Dto.Responses;

public class ProviderStatusResponse
{
    public string Provider { get; set; }
    public bool Enabled { get; set; }
    public bool Configured { get; set; }
    public string[] Roles { get; set; } = [];
    public bool Healthy { get; set; }
    public bool Degraded { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
    public string LastErrorCode { get; set; }
    public RateLimitStateResponse RateLimitState { get; set; }
    public Dictionary<string, ProviderCapabilityStatusResponse> Capabilities { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
