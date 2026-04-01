using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;

namespace Dxs.Consigliere.Data.Runtime;

public sealed class AdminRuntimeSourcePolicyService(IAdminProviderConfigService providerConfigService)
    : IAdminRuntimeSourcePolicyService
{
    public Task<Configs.ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
        => providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);

    public async Task<AdminRuntimeSourcesResponse> GetRuntimeSourcesAsync(CancellationToken cancellationToken = default)
    {
        var providers = await providerConfigService.GetProvidersAsync(cancellationToken);
        var config = providers.Config;

        return new AdminRuntimeSourcesResponse
        {
            RealtimePolicy = new AdminRealtimeSourcePolicyResponse
            {
                Static = BuildValue(config.Static),
                Override = config.Override is null ? null : BuildValue(config.Override),
                Effective = BuildValue(config.Effective),
                OverrideActive = config.OverrideActive,
                RestartRequired = config.RestartRequired,
                AllowedPrimarySources = config.AllowedRealtimePrimaryProviders,
                AllowedBitailsTransports = config.AllowedBitailsTransports,
                UpdatedAt = config.UpdatedAt,
                UpdatedBy = config.UpdatedBy
            }
        };
    }

    public async Task<AdminRealtimeSourcePolicyMutationResult> ApplyRealtimePolicyAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default)
    {
        var providers = await providerConfigService.GetProvidersAsync(cancellationToken);
        var effective = providers.Config.Effective;

        var result = await providerConfigService.ApplyProviderConfigAsync(
            new AdminProviderConfigUpdateRequest
            {
                RealtimePrimaryProvider = primaryRealtimeSource,
                RawTxPrimaryProvider = effective.RawTxPrimaryProvider,
                BitailsTransport = bitailsTransport,
                RestPrimaryProvider = effective.RestPrimaryProvider,
                Bitails = new AdminBitailsProviderConfigUpdateRequest
                {
                    ApiKey = effective.Bitails.ApiKey,
                    BaseUrl = effective.Bitails.BaseUrl,
                    WebsocketBaseUrl = effective.Bitails.WebsocketBaseUrl,
                    ZmqTxUrl = effective.Bitails.ZmqTxUrl,
                    ZmqBlockUrl = effective.Bitails.ZmqBlockUrl
                },
                Whatsonchain = new AdminRestProviderConfigUpdateRequest
                {
                    ApiKey = effective.Whatsonchain.ApiKey,
                    BaseUrl = effective.Whatsonchain.BaseUrl
                },
                Junglebus = new AdminJungleBusProviderConfigUpdateRequest
                {
                    BaseUrl = effective.Junglebus.BaseUrl,
                    MempoolSubscriptionId = effective.Junglebus.MempoolSubscriptionId,
                    BlockSubscriptionId = effective.Junglebus.BlockSubscriptionId
                }
            },
            updatedBy,
            cancellationToken);

        return new AdminRealtimeSourcePolicyMutationResult(result.Success, result.ErrorCode);
    }

    public async Task<AdminRuntimeSourcesResponse> ResetRealtimePolicyAsync(CancellationToken cancellationToken = default)
    {
        await providerConfigService.ResetProviderConfigAsync(cancellationToken);
        return await GetRuntimeSourcesAsync(cancellationToken);
    }

    private static AdminRealtimeSourcePolicyValuesResponse BuildValue(AdminProviderConfigValuesResponse values)
        => new()
        {
            PrimaryRealtimeSource = values.RealtimePrimaryProvider,
            BitailsTransport = values.BitailsTransport,
            FallbackSources = []
        };
}
