using Dxs.Consigliere.Data.Models.Runtime;

namespace Dxs.Consigliere.Data.Runtime;

public interface IRealtimeSourcePolicyOverrideStore
{
    Task<RealtimeSourcePolicyOverrideDocument> GetAsync(CancellationToken cancellationToken = default);
    Task<RealtimeSourcePolicyOverrideDocument> UpsertAsync(
        string primaryRealtimeSource,
        string bitailsTransport,
        string updatedBy,
        CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
}
