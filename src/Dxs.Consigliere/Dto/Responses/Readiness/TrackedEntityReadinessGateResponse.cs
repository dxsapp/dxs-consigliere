namespace Dxs.Consigliere.Dto.Responses.Readiness;

public class TrackedEntityReadinessGateResponse
{
    public string Code { get; set; } = "scope_not_ready";
    public TrackedEntityReadinessResponse[] Entities { get; set; } = [];
}
