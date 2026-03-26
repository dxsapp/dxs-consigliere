namespace Dxs.Consigliere.Data.Models.Tracking.HistoryBackfill;

public sealed class TokenHistoryAddressCursor
{
    public string Address { get; set; }
    public string Cursor { get; set; }
    public bool Completed { get; set; }
}
