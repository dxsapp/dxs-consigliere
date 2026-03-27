namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public interface IBitailsRealtimeSubscriptionScopeProvider
{
    Task<BitailsRealtimeSubscriptionScope> BuildAsync(CancellationToken cancellationToken = default);
}
