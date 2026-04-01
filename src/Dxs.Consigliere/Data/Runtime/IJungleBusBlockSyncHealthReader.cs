using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Data.Runtime;

public interface IJungleBusBlockSyncHealthReader
{
    Task<JungleBusBlockSyncStatusResponse> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
