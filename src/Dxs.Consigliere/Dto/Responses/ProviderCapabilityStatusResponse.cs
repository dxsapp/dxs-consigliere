namespace Dxs.Consigliere.Dto.Responses;

public class ProviderCapabilityStatusResponse
{
    public bool Enabled { get; set; }
    public bool Healthy { get; set; }
    public bool Degraded { get; set; }
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset? LastErrorAt { get; set; }
    public string LastErrorCode { get; set; }
    public RateLimitStateResponse RateLimitState { get; set; }
    public bool Active { get; set; }
}
