namespace Dxs.Consigliere.Data.Models.Transactions;

public static class TxLifecycleStatus
{
    public const string BroadcastSubmitted = "broadcast_submitted";
    public const string SeenBySource = "seen_by_source";
    public const string SeenInMempool = "seen_in_mempool";
    public const string Confirmed = "confirmed";
    public const string Reorged = "reorged";
    public const string Dropped = "dropped";
}
