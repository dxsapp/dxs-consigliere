using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Data.Runtime;

public interface IJungleBusChainTipAssuranceReader
{
    Task<JungleBusChainTipAssuranceResponse> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
