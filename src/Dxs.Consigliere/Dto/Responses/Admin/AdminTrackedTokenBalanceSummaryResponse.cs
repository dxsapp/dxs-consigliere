namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedTokenBalanceSummaryResponse
{
    public string TokenId { get; set; }
    public long Satoshis { get; set; }
}
