namespace Dxs.Consigliere.BackgroundTasks;

public interface IJungleBusMissingTransactionCollector
{
    Task CollectAsync(CancellationToken cancellationToken);
}
