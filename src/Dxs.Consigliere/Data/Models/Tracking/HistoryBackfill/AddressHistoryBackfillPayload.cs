namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public sealed class AddressHistoryBackfillPayload
{
    public int? AnchorBlockHeight { get; set; }
    public int? OldestCoveredBlockHeight { get; set; }
    public string Cursor { get; set; }
    public int DiscoveredTransactionCount { get; set; }
}
