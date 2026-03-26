using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Services.Impl;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class TrackedHistoryBackfillBackgroundTask(
    HistoricalAddressBackfillRunner addressRunner,
    HistoricalTokenBackfillRunner tokenRunner,
    IOptions<AppConfig> appConfig,
    ILogger<TrackedHistoryBackfillBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    protected override TimeSpan StartDelay => TimeSpan.FromSeconds(5);
    protected override TimeSpan Period => TimeSpan.FromSeconds(10);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(20);

    public override string Name => nameof(TrackedHistoryBackfillBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        if (await addressRunner.RunNextAsync(cancellationToken))
            return;

        await tokenRunner.RunNextAsync(cancellationToken);
    }
}
