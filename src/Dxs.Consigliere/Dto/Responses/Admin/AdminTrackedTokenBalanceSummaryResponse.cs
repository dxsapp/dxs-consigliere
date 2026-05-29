#nullable enable
namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminTrackedTokenBalanceSummaryResponse
{
    public string TokenId { get; set; } = string.Empty;
    public long Satoshis { get; set; }
}
