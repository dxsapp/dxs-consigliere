namespace Dxs.Consigliere.Dto.Requests;

public sealed class BulkTokenHistoryUpgradeRequest
{
    public TokenHistoryUpgradeRequest[] Items { get; set; } = [];
}
