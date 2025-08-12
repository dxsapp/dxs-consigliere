using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data;
using Dxs.Consigliere.Data.Models;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Raven.Client.Documents;

namespace Dxs.Consigliere.BackgroundTasks;

public class StasAttributesMissingTransactions(
    IDocumentStore store,
    IMetaTransactionStore transactionStore,
    IOptions<AppConfig> appConfig,
    ILogger<StasAttributesMissingTransactions> logger
): PeriodicTask(appConfig.Value.BackgroundTasks, logger)
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
            .Query<FoundMissingTransaction>()
            .Select(x => x.TxId)
            .ToListAsync(token: cancellationToken);

        if (!stasTransactions.Any())
            return;

        _logger.LogWarning("Found {Count} transaction without stas roots", stasTransactions.Count);

        foreach (var id in stasTransactions)
        {
            await transactionStore.UpdateStasAttributes(id);
        }
    }
}