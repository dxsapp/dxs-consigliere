using Dxs.Infrastructure.Bitails;
using Dxs.Infrastructure.Common;
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
            .AddSingleton<IExternalChainProviderDiagnostics, BitailsProviderDiagnostics>()
            .AddSingleton<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>()
            .AddHttpClient<IWhatsOnChainRestApiClient, WhatsOnChainRestApiClient>().Services
            .AddSingleton<IExternalChainProviderDiagnostics, WhatsOnChainProviderDiagnostics>()
            .AddTransient<JungleBusWebsocketClient>()
            .AddHttpClient<JungleBusWebsocketClient>().Services
            .AddSingleton<IExternalChainProviderDiagnostics, JungleBusProviderDiagnostics>()
            .AddSingleton<IExternalChainProviderCatalog, ExternalChainProviderCatalog>();
}
