using System;
using System.Collections.Generic;
using System.Linq;

namespace Dxs.Infrastructure.Bitails.Realtime;

public sealed record BitailsRealtimeTransportPlan(
    BitailsRealtimeTransportMode Mode,
    Uri Endpoint,
    IReadOnlyList<string> Topics
);

public sealed class BitailsRealtimeTransportPlanner : IBitailsRealtimeTransportPlanner
{
    public BitailsRealtimeTransportPlan CreateWebSocketPlan(params BitailsRealtimeSubscriptionTarget[] targets)
        => CreateWebSocketPlan(BitailsRealtimeTopicCatalog.DefaultWebSocketEndpoint, targets);

    public BitailsRealtimeTransportPlan CreateWebSocketPlan(Uri endpoint, params BitailsRealtimeSubscriptionTarget[] targets)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri)
            throw new ArgumentException("Bitails websocket transport requires an absolute endpoint URI.", nameof(endpoint));

        return new BitailsRealtimeTransportPlan(
            BitailsRealtimeTransportMode.WebSocket,
            endpoint,
            BitailsRealtimeTopicCatalog.GetTopics(targets ?? []).ToArray()
        );
    }

    public BitailsRealtimeTransportPlan CreateZmqPlan(Uri endpoint, params BitailsRealtimeSubscriptionTarget[] targets)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!endpoint.IsAbsoluteUri)
            throw new ArgumentException("Bitails ZMQ transport requires an absolute endpoint URI.", nameof(endpoint));

        return new BitailsRealtimeTransportPlan(
            BitailsRealtimeTransportMode.Zmq,
            endpoint,
            BitailsRealtimeTopicCatalog.GetZmqProxyTopics().ToArray()
        );
    }
}
