using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Dto.Responses.Admin;

namespace Dxs.Consigliere.Data.Runtime;

public interface IAdminRuntimeSourcePolicyService
{
    Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default);
    Task<AdminRuntimeSourcesResponse> GetRuntimeSourcesAsync(CancellationToken cancellationToken = default);
    Task<AdminRealtimeSourcePolicyMutationResult> ApplyRealtimePolicyAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default);
    Task<AdminRuntimeSourcesResponse> ResetRealtimePolicyAsync(CancellationToken cancellationToken = default);
}

public sealed record AdminRealtimeSourcePolicyMutationResult(bool Success, string ErrorCode = null);
