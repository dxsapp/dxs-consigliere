using Dxs.Infrastructure.Common;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class ExternalChainProviderSettingsAccessor(IAdminProviderConfigService providerConfigService)
    : IExternalChainProviderSettingsAccessor
{
    public async ValueTask<BitailsProviderRuntimeSettings> GetBitailsAsync(CancellationToken cancellationToken = default)
    {
        var effective = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        return new BitailsProviderRuntimeSettings(
            effective.Providers.Bitails.Connection.BaseUrl,
            effective.Providers.Bitails.Connection.ApiKey,
            effective.Providers.Bitails.Connection.Transport,
            effective.Providers.Bitails.Connection.Websocket.BaseUrl,
            effective.Providers.Bitails.Connection.Zmq.TxUrl,
            effective.Providers.Bitails.Connection.Zmq.BlockUrl
        );
    }

    public async ValueTask<WhatsOnChainProviderRuntimeSettings> GetWhatsOnChainAsync(CancellationToken cancellationToken = default)
    {
        var effective = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        return new WhatsOnChainProviderRuntimeSettings(
            effective.Providers.Whatsonchain.Connection.BaseUrl,
            effective.Providers.Whatsonchain.Connection.ApiKey
        );
    }

    public async ValueTask<JungleBusProviderRuntimeSettings> GetJungleBusAsync(CancellationToken cancellationToken = default)
    {
        var effective = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var jungleBus = await providerConfigService.GetEffectiveJungleBusAsync(cancellationToken);
        return new JungleBusProviderRuntimeSettings(
            jungleBus.BaseUrl ?? effective.Providers.JungleBus.Connection.BaseUrl,
            effective.Providers.JungleBus.Connection.ApiKey,
            jungleBus.MempoolSubscriptionId,
            jungleBus.BlockSubscriptionId
        );
    }
}
