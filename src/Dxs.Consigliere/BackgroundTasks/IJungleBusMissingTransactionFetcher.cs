namespace Dxs.Consigliere.BackgroundTasks;

public interface IJungleBusMissingTransactionFetcher
{
    Task FetchAsync(CancellationToken cancellationToken);
}
