namespace Dxs.Consigliere.Dto.Requests;

public sealed class BulkTokenHistoryUpgradeRequest
{
    public string[] TokenIds { get; set; } = [];
}
