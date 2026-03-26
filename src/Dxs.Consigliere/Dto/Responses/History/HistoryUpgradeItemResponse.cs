namespace Dxs.Consigliere.Dto.Responses.History;

public sealed class HistoryUpgradeItemResponse
{
    public string EntityId { get; set; }
    public bool Accepted { get; set; }
    public string MessageCode { get; set; }
    public TrackedHistoryStatusResponse History { get; set; }
}
