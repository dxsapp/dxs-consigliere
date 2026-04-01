namespace Dxs.Consigliere.Services;

public sealed record RawTransactionFetchResult(string Provider, byte[] Raw);

public interface IRawTransactionFetchService
{
    Task<RawTransactionFetchResult> GetAsync(string txId, CancellationToken cancellationToken = default);
    Task<RawTransactionFetchResult> TryGetAsync(string txId, CancellationToken cancellationToken = default);
}
