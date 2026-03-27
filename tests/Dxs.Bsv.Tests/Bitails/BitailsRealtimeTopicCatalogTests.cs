using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Bsv.Tests.Bitails;

public class BitailsRealtimeTopicCatalogTests
{
    [Fact]
    public void GetTopics_ExpandsManagedScopeTargetsIntoCanonicalBitailsTopics()
    {
        var topics = BitailsRealtimeTopicCatalog.GetTopics(
        [
            new BitailsRealtimeSubscriptionTarget.AllTransactions(),
            new BitailsRealtimeSubscriptionTarget.AllAddresses(),
            new BitailsRealtimeSubscriptionTarget.AllScripthashes(),
            new BitailsRealtimeSubscriptionTarget.Transaction("tx-123"),
            new BitailsRealtimeSubscriptionTarget.AddressLock("1BitailsLockAddress"),
            new BitailsRealtimeSubscriptionTarget.AddressSpent("1BitailsSpentAddress"),
            new BitailsRealtimeSubscriptionTarget.ScripthashLock("lockhash"),
            new BitailsRealtimeSubscriptionTarget.ScripthashSpent("spendhash"),
            new BitailsRealtimeSubscriptionTarget.UtxoSpent("utxo-tx", 7)
        ]);

        Assert.Equal(
        [
            "tx",
            "lock-address",
            "spent-address",
            "lock-scripthash",
            "spent-scripthash",
            "tx-tx-123",
            "lock-address-1BitailsLockAddress",
            "spent-address-1BitailsSpentAddress",
            "lock-scripthash-lockhash",
            "spent-scripthash-spendhash",
            "utxo-spent-utxo-tx_7"
        ],
        topics);
    }

    [Fact]
    public void GetTopics_DeduplicatesRepeatedTopicsWithoutChangingOrder()
    {
        var topics = BitailsRealtimeTopicCatalog.GetTopics(
        [
            new BitailsRealtimeSubscriptionTarget.AllAddresses(),
            new BitailsRealtimeSubscriptionTarget.AddressLock("1RepeatAddress"),
            new BitailsRealtimeSubscriptionTarget.AddressSpent("1RepeatAddress"),
            new BitailsRealtimeSubscriptionTarget.AddressLock("1RepeatAddress")
        ]);

        Assert.Equal(
        [
            "lock-address",
            "spent-address",
            "lock-address-1RepeatAddress",
            "spent-address-1RepeatAddress"
        ],
        topics);
    }

    [Fact]
    public void GetTopics_RejectsBlankTargets()
    {
        Assert.Throws<ArgumentException>(() => BitailsRealtimeTopicCatalog.GetTopics(new BitailsRealtimeSubscriptionTarget.Transaction(" ")));
        Assert.Throws<ArgumentException>(() => BitailsRealtimeTopicCatalog.GetTopics(new BitailsRealtimeSubscriptionTarget.AddressLock("")));
        Assert.Throws<ArgumentOutOfRangeException>(() => BitailsRealtimeTopicCatalog.GetTopics(new BitailsRealtimeSubscriptionTarget.UtxoSpent("tx", -1)));
    }
}
