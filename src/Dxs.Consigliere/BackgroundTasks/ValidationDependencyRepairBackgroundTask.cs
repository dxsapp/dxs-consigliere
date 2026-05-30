using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks;

public sealed class ValidationDependencyRepairBackgroundTask(
    IValidationDependencyRepairProcessor processor,
    IOptions<AppConfig> appConfig,
    ILogger<ValidationDependencyRepairBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    protected override TimeSpan Period => TimeSpan.FromSeconds(10);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(15);

    public override string Name => nameof(ValidationDependencyRepairBackgroundTask);

    protected override Task RunAsync(CancellationToken cancellationToken)
    {
        // Policy: validate with locally-available data only — no on-the-fly
        // reverse-lineage ancestor fetching from external providers. Default
        // off; flip Consigliere:Validation:ReverseLineageRepairEnabled to
        // re-enable provider-backed backfill.
        if (!appConfig.Value.Validation.ReverseLineageRepairEnabled)
            return Task.CompletedTask;

        return processor.ProcessDueAsync(cancellationToken);
    }
}
