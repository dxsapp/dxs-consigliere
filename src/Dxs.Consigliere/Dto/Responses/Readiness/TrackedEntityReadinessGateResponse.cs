namespace Dxs.Consigliere.Dto.Responses.Readiness;

public class TrackedEntityReadinessGateResponse
{
    public string Code { get; set; } = "not_ready";
    public TrackedEntityReadinessResponse[] Entities { get; set; } = [];
}
