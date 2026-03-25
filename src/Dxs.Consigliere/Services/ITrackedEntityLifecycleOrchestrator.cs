namespace Dxs.Consigliere.Services;

public interface ITrackedEntityLifecycleOrchestrator
{
    Task BeginTrackingAddressAsync(string address, CancellationToken cancellationToken = default);
    Task BeginTrackingTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressBackfillCompletedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenBackfillCompletedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressGapClosedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenGapClosedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressDegradedAsync(string address, bool integritySafe, string reason = null, CancellationToken cancellationToken = default);
    Task MarkTokenDegradedAsync(string tokenId, bool integritySafe, string reason = null, CancellationToken cancellationToken = default);
    Task RecoverAddressAsync(string address, CancellationToken cancellationToken = default);
    Task RecoverTokenAsync(string tokenId, CancellationToken cancellationToken = default);
}
