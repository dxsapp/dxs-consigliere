using Dxs.Common.BackgroundTasks;
using Dxs.Common.Extensions;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Extensions;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;
using Dxs.Infrastructure.JungleBus;
using Dxs.Infrastructure.JungleBus.Dto;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.Blocks;

public sealed class JungleBusBlockSyncMonitorBackgroundTask(
    IServiceProvider serviceProvider,
    IJungleBusBlockSyncScheduler scheduler,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
    IAdminProviderConfigService providerConfigService,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<JungleBusBlockSyncMonitorBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => Timeout.InfiniteTimeSpan;
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(10);
    public override string Name => nameof(JungleBusBlockSyncMonitorBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var effectiveSources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            effectiveSources,
            appConfig.Value,
            providerCatalog
        );

        if (!string.Equals(route.PrimarySource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping JungleBus block-sync monitor because block-backfill primary is `{Primary}`",
                route.PrimarySource
            );
            return;
        }

        var jungleBus = await providerConfigService.GetEffectiveJungleBusAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(jungleBus.BlockSubscriptionId))
        {
            _logger.LogWarning(
                "Skipping JungleBus block-sync monitor because block subscription ID is not configured"
            );
            return;
        }

        using var scopedServices = serviceProvider.GetScopedService(out JungleBusWebsocketClient websocketClient);
        using var gate = new SemaphoreSlim(1, 1);
        using var __ = _logger.BeginScope("JungleBusBlockSyncMonitor");

        await websocketClient.StartSubscription(jungleBus.BlockSubscriptionId);

        Exception failure = null;

        using var controlSubscription = websocketClient.ControlMessages.Subscribe(
            message =>
            {
                if (message.Block <= 0)
                    return;

                if (message.Code is not ((int)PubControlMessageDto.StatusCode.Waiting or (int)PubControlMessageDto.StatusCode.BlockDone))
                    return;

                _ = Task.Run(async () =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        await scheduler.ScheduleUpToHeightAsync(message.Block, jungleBus.BlockSubscriptionId, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to schedule JungleBus block sync for height {Height}", message.Block);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, cancellationToken);
            },
            exception => failure = exception
        );

        websocketClient.SubscribeToControlMessages();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (failure is not null)
                throw failure;

            if (!websocketClient.IsRunning)
                throw new InvalidOperationException("JungleBus block-sync websocket stopped unexpectedly");

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }
}
