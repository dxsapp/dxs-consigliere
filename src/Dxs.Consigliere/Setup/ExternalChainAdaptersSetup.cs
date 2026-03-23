using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.WoC;

using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Setup;

public static class ExternalChainAdaptersSetup
{
    public static IServiceCollection AddExternalChainAdapterZoneServices(this IServiceCollection services)
        => services
            .AddTransient<IBitailsRestApiClient, BitailsRestApiClient>()
            .AddHttpClient<IBitailsRestApiClient, BitailsRestApiClient>().Services
            .AddSingleton<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>()
            .AddHttpClient<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>().Services
            .AddTransient<JungleBusWebsocketClient>()
            .AddHttpClient<JungleBusWebsocketClient>().Services;
}
