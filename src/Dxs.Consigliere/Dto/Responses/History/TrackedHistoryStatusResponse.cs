namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class TrackedHistoryStatusResponse
{
    public string HistoryReadiness { get; set; }
    public TrackedHistoryCoverageResponse Coverage { get; set; }
    public TrackedHistoryBackfillStatusResponse BackfillStatus { get; set; }
}
