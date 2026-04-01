using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.BackgroundTasks;

public interface IValidationDependencyRepairScheduler
{
    Task<ValidationRepairWorkItemDocument?> ScheduleTransactionAsync(
        string txId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ValidationRepairWorkItemDocument?> GetScheduledTransactionAsync(
        string txId,
        CancellationToken cancellationToken = default);

    Task CancelTransactionAsync(
        string txId,
        CancellationToken cancellationToken = default);
}
