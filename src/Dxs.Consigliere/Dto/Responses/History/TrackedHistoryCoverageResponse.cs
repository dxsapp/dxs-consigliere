#nullable enable
namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class TrackedHistoryCoverageResponse
{
    public string Mode { get; set; } = string.Empty;
    public bool FullCoverage { get; set; }
    public int? AuthoritativeFromBlockHeight { get; set; }
    public long? AuthoritativeFromObservedAt { get; set; }
}
