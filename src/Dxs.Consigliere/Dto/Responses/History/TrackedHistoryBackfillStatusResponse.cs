namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class TrackedHistoryBackfillStatusResponse
{
    public string Status { get; set; }
    public long? RequestedAt { get; set; }
    public long? StartedAt { get; set; }
    public long? LastProgressAt { get; set; }
    public long? CompletedAt { get; set; }
    public int ItemsScanned { get; set; }
    public int ItemsApplied { get; set; }
    public string ErrorCode { get; set; }
}
