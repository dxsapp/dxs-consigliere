using Dxs.Consigliere.Dto.Responses.Readiness;

namespace Dxs.Consigliere.Services;

public interface ITrackedEntityReadinessService
{
    Task<TrackedEntityReadinessResponse> GetAddressReadinessAsync(string address, CancellationToken cancellationToken = default);
    Task<TrackedEntityReadinessResponse> GetTokenReadinessAsync(string tokenId, CancellationToken cancellationToken = default);
    Task<TrackedEntityReadinessGateResponse> GetBlockingReadinessAsync(
        IEnumerable<string> addresses,
        IEnumerable<string> tokenIds,
        CancellationToken cancellationToken = default
    );
}
