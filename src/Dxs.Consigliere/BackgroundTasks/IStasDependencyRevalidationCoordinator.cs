namespace Dxs.Consigliere.BackgroundTasks;

public interface IStasDependencyRevalidationCoordinator
{
    Task HandleTransactionChangedAsync(string txId, CancellationToken cancellationToken = default);
    Task HandleTransactionDeletedAsync(string txId, CancellationToken cancellationToken = default);
}
