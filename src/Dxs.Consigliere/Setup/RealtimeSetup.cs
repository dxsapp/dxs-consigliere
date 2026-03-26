using Dxs.Consigliere.Notifications;
using Dxs.Consigliere.Services;
using Dxs.Consigliere.Services.Impl;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class RealtimeSetup
{
    public static IServiceCollection AddRealtimeZoneServices(this IServiceCollection services)
        => services
            .AddSingleton<TransactionNotificationDispatcher>()
            .AddSingleton<IRealtimeEventDispatcher, RealtimeEventDispatcher>()
            .AddSingleton<ManagedScopeRealtimeNotifier>()
            .AddSingleton<ConnectionManager>()
            .AddSingleton<IConnectionManager>(sp => sp.GetRequiredService<ConnectionManager>())
            .AddSingleton<INotificationHandler<BlockProcessed>>(sp => sp.GetRequiredService<ConnectionManager>())
            .AddSingleton<INotificationHandler<TransactionDeleted>>(sp => sp.GetRequiredService<ConnectionManager>())
            .AddSingleton<IBroadcastService, BroadcastService>();
}
