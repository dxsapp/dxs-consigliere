using Dxs.Consigliere.Data.Models.Transactions;

namespace Dxs.Consigliere.Data.Transactions;

public interface IValidationRepairWorkItemStore
{
    Task<ValidationRepairWorkItemDocument> ScheduleAsync(
        string txId,
        string reason,
        IReadOnlyCollection<string> missingDependencies,
        CancellationToken cancellationToken = default);

    Task<ValidationRepairWorkItemDocument> MarkRunningAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        CancellationToken cancellationToken = default);

    Task<ValidationRepairWorkItemDocument?> MarkResolvedAsync(
        string txId,
        CancellationToken cancellationToken = default);

    Task<ValidationRepairWorkItemDocument?> MarkRetryAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        string lastError,
        DateTimeOffset nextAttemptAt,
        bool failed,
        CancellationToken cancellationToken = default);
    Task<ValidationRepairWorkItemDocument?> MarkBlockedAsync(
        string txId,
        IReadOnlyCollection<string> missingDependencies,
        string lastError,
        CancellationToken cancellationToken = default);

    Task<ValidationRepairWorkItemDocument?> LoadAsync(string txId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ValidationRepairWorkItemDocument>> LoadDueAsync(int take, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ValidationRepairWorkItemDocument>> LoadActiveAsync(int take, CancellationToken cancellationToken = default);
    Task RemoveAsync(string txId, CancellationToken cancellationToken = default);
}
