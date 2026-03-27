namespace Dxs.Infrastructure.Bitails.Realtime;

public abstract record BitailsRealtimeSubscriptionTarget
{
    protected BitailsRealtimeSubscriptionTarget() { }

    public sealed record AllTransactions : BitailsRealtimeSubscriptionTarget;
    public sealed record AllAddresses : BitailsRealtimeSubscriptionTarget;
    public sealed record AllScripthashes : BitailsRealtimeSubscriptionTarget;
    public sealed record Transaction(string TxId) : BitailsRealtimeSubscriptionTarget;
    public sealed record AddressLock(string Address) : BitailsRealtimeSubscriptionTarget;
    public sealed record AddressSpent(string Address) : BitailsRealtimeSubscriptionTarget;
    public sealed record ScripthashLock(string Scripthash) : BitailsRealtimeSubscriptionTarget;
    public sealed record ScripthashSpent(string Scripthash) : BitailsRealtimeSubscriptionTarget;
    public sealed record UtxoSpent(string TxId, int OutputIndex) : BitailsRealtimeSubscriptionTarget;
}
