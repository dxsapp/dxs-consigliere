namespace Dxs.Consigliere.Services;

using Dxs.Consigliere.Data.Models.Tracking;

public interface ITrackedEntityLifecycleOrchestrator
{
    Task BeginTrackingAddressAsync(string address, CancellationToken cancellationToken = default);
    Task BeginTrackingTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressHistoryBackfillQueuedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenHistoryBackfillQueuedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressHistoryBackfillRunningAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenHistoryBackfillRunningAsync(string tokenId, CancellationToken cancellationToken = default);
    Task UpdateAddressHistoryBackfillProgressAsync(
        string address,
        int itemsScanned,
        int itemsApplied,
        int? oldestCoveredBlockHeight,
        CancellationToken cancellationToken = default
    );
    Task UpdateTokenHistoryBackfillProgressAsync(
        string tokenId,
        int itemsScanned,
        int itemsApplied,
        int? oldestCoveredBlockHeight,
        CancellationToken cancellationToken = default
    );
    Task MarkAddressHistoryBackfillWaitingRetryAsync(string address, string errorCode, CancellationToken cancellationToken = default);
    Task MarkTokenHistoryBackfillWaitingRetryAsync(string tokenId, string errorCode, CancellationToken cancellationToken = default);
    Task MarkAddressHistoryBackfillCompletedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenHistoryBackfillCompletedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressHistoryBackfillFailedAsync(string address, string errorCode, CancellationToken cancellationToken = default);
    Task MarkTokenHistoryBackfillFailedAsync(string tokenId, string errorCode, CancellationToken cancellationToken = default);
    Task MarkAddressBackfillCompletedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenBackfillCompletedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressGapClosedAsync(string address, CancellationToken cancellationToken = default);
    Task MarkTokenGapClosedAsync(string tokenId, CancellationToken cancellationToken = default);
    Task MarkAddressDegradedAsync(string address, bool integritySafe, string reason = null, CancellationToken cancellationToken = default);
    Task MarkTokenDegradedAsync(string tokenId, bool integritySafe, string reason = null, CancellationToken cancellationToken = default);
    Task RecoverAddressAsync(string address, CancellationToken cancellationToken = default);
    Task RecoverTokenAsync(string tokenId, CancellationToken cancellationToken = default);
    Task UpdateTokenHistorySecurityAsync(
        string tokenId,
        TrackedTokenHistorySecurityState historySecurity,
        CancellationToken cancellationToken = default
    );
}
