using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Addresses;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class AddressHistoryEnvelopeBackfillBackgroundTask(
    IAddressHistoryEnvelopeBackfillService backfillService,
    IOptions<AppConfig> appConfig,
    ILogger<AddressHistoryEnvelopeBackfillBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    protected override TimeSpan StartDelay => TimeSpan.FromSeconds(10);
    protected override TimeSpan Period => TimeSpan.FromSeconds(20);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(20);

    public override string Name => nameof(AddressHistoryEnvelopeBackfillBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var result = await backfillService.BackfillBatchAsync(128, cancellationToken);
        if (result.Rewritten <= 0 && result.PendingCount <= 0)
            return;

        logger.LogInformation(
            "{TaskName} scanned {Scanned}, rewrote {Rewritten}, missing-meta {MissingTransactions}, pending {PendingCount}",
            Name,
            result.Scanned,
            result.Rewritten,
            result.MissingTransactions,
            result.PendingCount);
    }
}
