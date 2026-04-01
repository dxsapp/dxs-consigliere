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
        => processor.ProcessDueAsync(cancellationToken);
}
