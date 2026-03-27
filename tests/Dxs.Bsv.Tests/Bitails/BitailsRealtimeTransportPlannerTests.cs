using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Bsv.Tests.Bitails;

public class BitailsRealtimeTransportPlannerTests
{
    [Fact]
    public void CreateWebSocketPlan_UsesBitailsGlobalEndpointAndCanonicalTopics()
    {
        var planner = new BitailsRealtimeTransportPlanner();
        var plan = planner.CreateWebSocketPlan(
            new BitailsRealtimeSubscriptionTarget.AllTransactions(),
            new BitailsRealtimeSubscriptionTarget.AddressLock("1BitailsLockAddress"),
            new BitailsRealtimeSubscriptionTarget.Transaction("tx-123")
        );

        Assert.Equal(BitailsRealtimeTransportMode.WebSocket, plan.Mode);
        Assert.Equal(BitailsRealtimeTopicCatalog.DefaultWebSocketEndpoint, plan.Endpoint);
        Assert.Equal(
        [
            "tx",
            "lock-address-1BitailsLockAddress",
            "tx-tx-123"
        ],
        plan.Topics);
    }

    [Fact]
    public void CreateZmqPlan_UsesExplicitOperatorEndpoint()
    {
        var planner = new BitailsRealtimeTransportPlanner();
        var endpoint = new Uri("tcp://127.0.0.1:28332");

        var plan = planner.CreateZmqPlan(endpoint,
            new BitailsRealtimeSubscriptionTarget.AllAddresses(),
            new BitailsRealtimeSubscriptionTarget.UtxoSpent("utxo-tx", 7)
        );

        Assert.Equal(BitailsRealtimeTransportMode.Zmq, plan.Mode);
        Assert.Equal(endpoint, plan.Endpoint);
        Assert.Equal(
        [
            "lock-address",
            "spent-address",
            "utxo-spent-utxo-tx_7"
        ],
        plan.Topics);
    }
}
