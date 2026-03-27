using Dxs.Consigliere.Dto.Responses.Readiness;

namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedTokenResponse
{
    public string TokenId { get; set; }
    public string Symbol { get; set; }
    public bool IsTombstoned { get; set; }
    public long? TombstonedAt { get; set; }
    public long CreatedAt { get; set; }
    public long? UpdatedAt { get; set; }
    public string FailureReason { get; set; }
    public bool? IntegritySafe { get; set; }
    public TrackedEntityReadinessResponse Readiness { get; set; }
}
