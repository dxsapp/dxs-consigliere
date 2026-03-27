using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

public sealed class RealtimeIngestBackgroundTask(
    JungleBusRealtimeIngestRunner jungleBusRunner,
    BitailsRealtimeIngestRunner bitailsRunner,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<RealtimeIngestBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly AppConfig _appConfig = appConfig.Value;
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => Timeout.InfiniteTimeSpan;
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(10);

    public override string Name => nameof(RealtimeIngestBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken),
            _appConfig,
            providerCatalog
        );
        var selectedSource = route.PrimarySource;

        _logger.LogInformation(
            "Selected realtime ingest source `{Source}`; primary `{Primary}`; fallbacks {Fallbacks}",
            selectedSource,
            route.PrimarySource,
            route.FallbackSources);

        if (string.Equals(selectedSource, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            await bitailsRunner.RunAsync(cancellationToken);
            return;
        }

        if (string.Equals(selectedSource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
        {
            await jungleBusRunner.RunAsync(cancellationToken);
            return;
        }

        if (string.Equals(selectedSource, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Realtime ingest is delegated to node/ZMQ startup wiring.");
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return;
        }

        throw new InvalidOperationException($"Unsupported realtime ingest source '{selectedSource}'.");
    }
}
