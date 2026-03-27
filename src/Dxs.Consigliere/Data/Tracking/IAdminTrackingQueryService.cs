using Dxs.Consigliere.Dto.Responses.Admin;

namespace Dxs.Consigliere.Data.Tracking;

public interface IAdminTrackingQueryService
{
    Task<AdminTrackedAddressResponse[]> GetTrackedAddressesAsync(bool includeTombstoned = false, CancellationToken cancellationToken = default);
    Task<AdminTrackedTokenResponse[]> GetTrackedTokensAsync(bool includeTombstoned = false, CancellationToken cancellationToken = default);
    Task<AdminTrackedAddressResponse> GetTrackedAddressAsync(string address, CancellationToken cancellationToken = default);
    Task<AdminTrackedTokenResponse> GetTrackedTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task<AdminFindingResponse[]> GetFindingsAsync(int take = 100, CancellationToken cancellationToken = default);
    Task<AdminDashboardSummaryResponse> GetDashboardSummaryAsync(CancellationToken cancellationToken = default);
}
