using Dxs.Consigliere.Data.Models.Tracking;

namespace Dxs.Consigliere.Data.Tracking;

public interface ITrackedEntityRegistrationStore
{
    Task RegisterAddressAsync(
        string address,
        string name,
        string historyMode = TrackedEntityHistoryMode.ForwardOnly,
        CancellationToken cancellationToken = default
    );
    Task RegisterTokenAsync(
        string tokenId,
        string symbol,
        string historyMode = TrackedEntityHistoryMode.ForwardOnly,
        CancellationToken cancellationToken = default
    );
    Task<bool> RequestAddressFullHistoryAsync(string address, CancellationToken cancellationToken = default);
    Task<bool> RequestTokenFullHistoryAsync(string tokenId, CancellationToken cancellationToken = default);
}
