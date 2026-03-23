namespace Dxs.Consigliere.BackgroundTasks;

public interface IJungleBusSyncRequestProcessor
{
    Task<bool> ProcessAsync(CancellationToken cancellationToken);
}
