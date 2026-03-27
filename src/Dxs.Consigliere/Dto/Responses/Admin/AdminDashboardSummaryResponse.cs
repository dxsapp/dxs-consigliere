namespace Dxs.Consigliere.Dto.Responses.Admin;

public sealed class AdminDashboardSummaryResponse
{
    public int ActiveAddressCount { get; set; }
    public int ActiveTokenCount { get; set; }
    public int TombstonedAddressCount { get; set; }
    public int TombstonedTokenCount { get; set; }
    public int DegradedAddressCount { get; set; }
    public int DegradedTokenCount { get; set; }
    public int BackfillingAddressCount { get; set; }
    public int BackfillingTokenCount { get; set; }
    public int FullHistoryLiveAddressCount { get; set; }
    public int FullHistoryLiveTokenCount { get; set; }
    public int UnknownRootFindingCount { get; set; }
    public int BlockingUnknownRootTokenCount { get; set; }
    public int FailureCount { get; set; }
}
