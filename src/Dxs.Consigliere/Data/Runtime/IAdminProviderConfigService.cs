using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;

namespace Dxs.Consigliere.Data.Runtime;

public interface IAdminProviderConfigService
{
    Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default);
    Task<JungleBusProviderRuntimeSnapshot> GetEffectiveJungleBusAsync(CancellationToken cancellationToken = default);
    Task<AdminProvidersResponse> GetProvidersAsync(CancellationToken cancellationToken = default);
    Task<AdminProviderConfigMutationResult> ApplyProviderConfigAsync(
        AdminProviderConfigUpdateRequest request,
        string updatedBy,
        CancellationToken cancellationToken = default);
    Task<AdminProvidersResponse> ResetProviderConfigAsync(CancellationToken cancellationToken = default);
}

public sealed record AdminProviderConfigMutationResult(bool Success, string ErrorCode = null);

public sealed record JungleBusProviderRuntimeSnapshot(
    string BaseUrl,
    string MempoolSubscriptionId,
    string BlockSubscriptionId
);
