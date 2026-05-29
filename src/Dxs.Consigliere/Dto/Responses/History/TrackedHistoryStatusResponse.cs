#nullable enable
namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class TrackedHistoryStatusResponse
{
    public string HistoryReadiness { get; set; } = string.Empty;
    public TrackedHistoryCoverageResponse? Coverage { get; set; }
    public TrackedHistoryBackfillStatusResponse? BackfillStatus { get; set; }
    public RootedTokenHistoryStatusResponse? RootedToken { get; set; }
}
