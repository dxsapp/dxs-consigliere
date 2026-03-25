namespace Dxs.Consigliere.Data.Tracking;

public interface ITrackedEntityRegistrationStore
{
    Task RegisterAddressAsync(string address, string name, CancellationToken cancellationToken = default);
    Task RegisterTokenAsync(string tokenId, string symbol, CancellationToken cancellationToken = default);
}
