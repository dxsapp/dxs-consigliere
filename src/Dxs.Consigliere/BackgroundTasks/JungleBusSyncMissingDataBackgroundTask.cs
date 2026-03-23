using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public class JungleBusSyncMissingDataBackgroundTask(
    IJungleBusSyncRequestProcessor syncRequestProcessor,
    IJungleBusMissingTransactionCollector missingTransactionCollector,
    IJungleBusMissingTransactionFetcher missingTransactionFetcher,
    IOptions<AppConfig> appConfig,
    ILogger<JungleBusSyncMissingDataBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    protected override TimeSpan Period => TimeSpan.FromSeconds(5);

    protected override TimeSpan WaitTimeOnError => TimeSpan.FromMinutes(5);

    public override string Name => nameof(JungleBusSyncMissingDataBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await syncRequestProcessor.ProcessAsync(cancellationToken))
        {
            await missingTransactionCollector.CollectAsync(cancellationToken);
            await missingTransactionFetcher.FetchAsync(cancellationToken);
        }
    }
}
