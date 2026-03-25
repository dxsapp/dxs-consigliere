namespace Dxs.Consigliere.Dto.Responses;

public class RateLimitStateResponse
{
    public bool Limited { get; set; }
    public int? Remaining { get; set; }
    public DateTimeOffset? ResetAt { get; set; }
    public string Scope { get; set; }
    public string SourceHint { get; set; }
}
