using Dxs.Consigliere.Dto.Responses;

namespace Dxs.Consigliere.Data.Runtime;

public interface IValidationRepairStatusReader
{
    Task<ValidationRepairStatusResponse> GetSnapshotAsync(CancellationToken cancellationToken = default);
}
