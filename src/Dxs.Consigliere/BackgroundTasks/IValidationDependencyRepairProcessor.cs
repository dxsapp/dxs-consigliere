namespace Dxs.Consigliere.BackgroundTasks;

public interface IValidationDependencyRepairProcessor
{
    Task<int> ProcessDueAsync(CancellationToken cancellationToken = default);
}
