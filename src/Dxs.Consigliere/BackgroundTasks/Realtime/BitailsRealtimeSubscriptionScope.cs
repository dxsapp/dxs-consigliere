using Dxs.Infrastructure.Bitails.Realtime;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed record BitailsRealtimeSubscriptionScope(
    string Signature,
    BitailsRealtimeTransportPlan TransportPlan,
    int AddressCount,
    int TokenCount,
    bool UsesGlobalTransactions
);
