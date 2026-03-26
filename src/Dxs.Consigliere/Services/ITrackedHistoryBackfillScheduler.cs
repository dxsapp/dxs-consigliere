namespace Dxs.Consigliere.Services;

public interface ITrackedHistoryBackfillScheduler
{
    Task<bool> QueueAddressFullHistoryAsync(string address, CancellationToken cancellationToken = default);
    Task<bool> QueueTokenFullHistoryAsync(string tokenId, CancellationToken cancellationToken = default);
}
