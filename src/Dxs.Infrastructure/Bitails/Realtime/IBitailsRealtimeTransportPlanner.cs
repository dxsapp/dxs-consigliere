using System;

namespace Dxs.Infrastructure.Bitails.Realtime;

public interface IBitailsRealtimeTransportPlanner
{
    BitailsRealtimeTransportPlan CreateWebSocketPlan(params BitailsRealtimeSubscriptionTarget[] targets);
    BitailsRealtimeTransportPlan CreateZmqPlan(Uri endpoint, params BitailsRealtimeSubscriptionTarget[] targets);
}
