using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models.Transactions;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class StasAttributesMissingTransactions(
    IDocumentStore store,
    IStasDependencyRevalidationCoordinator coordinator,
    IValidationDependencyRepairScheduler validationRepairScheduler,
    IOptions<AppConfig> appConfig,
    ILogger<StasAttributesMissingTransactions> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => TimeSpan.FromSeconds(30);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(30);

    public override string Name => nameof(StasAttributesMissingTransactions);

    protected override Task RunAsync(CancellationToken cancellationToken)
        => FindMissingStasTransactions(cancellationToken);

    private async Task FindMissingStasTransactions(CancellationToken cancellationToken)
    {
        using var session = store.GetNoCacheNoTrackingSession();

        var stasTransactions = await session
            .Query<MetaTransaction>()
            .Where(x => x.MissingTransactions.Count > 0)
            .Select(x => x.Id)
            .ToListAsync(token: cancellationToken);

        if (!stasTransactions.Any())
            return;

        _logger.LogWarning("Found {Count} transaction with unresolved STAS dependencies", stasTransactions.Count);

        foreach (var id in stasTransactions)
        {
            await validationRepairScheduler.ScheduleTransactionAsync(id, ValidationRepairReasons.MissingParentRepair, cancellationToken);
            await coordinator.HandleTransactionChangedAsync(id, cancellationToken);
        }
    }
}
