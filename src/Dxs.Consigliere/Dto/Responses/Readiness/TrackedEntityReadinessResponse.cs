#nullable enable
using Dxs.Consigliere.Dto.Responses.History;

namespace Dxs.Consigliere.Dto.Responses.Readiness;

public class TrackedEntityReadinessResponse
{
    public bool Tracked { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string LifecycleStatus { get; set; } = string.Empty;
    public bool Readable { get; set; }
    public bool Authoritative { get; set; }
    public bool Degraded { get; set; }
    public int? LagBlocks { get; set; }
    public double? Progress { get; set; }
    public TrackedHistoryStatusResponse History { get; set; } = new();
}
