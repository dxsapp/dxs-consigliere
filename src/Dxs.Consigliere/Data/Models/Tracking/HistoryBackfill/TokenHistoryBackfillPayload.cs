namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public sealed class TokenHistoryBackfillPayload
{
    public int? AnchorBlockHeight { get; set; }
    public int? OldestCoveredBlockHeight { get; set; }
    public string Cursor { get; set; }
    public int DiscoveredTransactionCount { get; set; }
    public bool LineageBoundaryReached { get; set; }
    public bool HistoryBoundaryReached { get; set; }
}
