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
    IJungleBusBlockSyncHealthStore healthStore,
    IAdminRuntimeSourcePolicyService runtimeSourcePolicyService,
    IAdminProviderConfigService providerConfigService,
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<JungleBusBlockSyncMonitorBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => TimeSpan.FromSeconds(2);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(10);
    public override string Name => nameof(JungleBusBlockSyncMonitorBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(cancellationToken);

        if (!string.Equals(route.PrimarySource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogDebug("JungleBus block-sync monitor idle because block-backfill primary is `{Primary}`", route.PrimarySource);
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
        var signature = await BuildSignatureAsync(route, cancellationToken);

        Exception failure = null;

        using var controlSubscription = websocketClient.ControlMessages.Subscribe(
            async message =>
            {
                await healthStore.TouchControlMessageAsync(jungleBus.BlockSubscriptionId, message, cancellationToken);

                if (message.Block <= 0)
                    return;

                if (message.Code is not ((int)PubControlMessageDto.StatusCode.Waiting or (int)PubControlMessageDto.StatusCode.BlockDone))
                    return;

                _ = Task.Run(async () =>
                {
                    await gate.WaitAsync(cancellationToken);
                    try
                    {
                        var result = await scheduler.ScheduleUpToHeightAsync(message.Block, jungleBus.BlockSubscriptionId, cancellationToken);
                        await healthStore.TouchScheduledAsync(jungleBus.BlockSubscriptionId, result, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Failed to schedule JungleBus block sync for height {Height}", message.Block);
                        await healthStore.RecordErrorAsync(exception.Message, cancellationToken);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, cancellationToken);
            },
            async exception =>
            {
                failure = exception;
                await healthStore.RecordErrorAsync(exception.Message, cancellationToken);
            }
        );

        websocketClient.SubscribeToControlMessages();

        while (!cancellationToken.IsCancellationRequested)
        {
            if (failure is not null)
                throw failure;

            if (!websocketClient.IsRunning)
                throw new InvalidOperationException("JungleBus block-sync websocket stopped unexpectedly");

            var currentRoute = await ResolveRouteAsync(cancellationToken);
            var currentSignature = await BuildSignatureAsync(currentRoute, cancellationToken);
            if (!string.Equals(signature, currentSignature, StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "JungleBus block-sync configuration changed from `{OldSignature}` to `{NewSignature}`; recycling runtime task",
                    signature,
                    currentSignature
                );
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }
    }

    private async Task<SourceCapabilityRoute> ResolveRouteAsync(CancellationToken cancellationToken)
        => SourceCapabilityRouting.Resolve(
            ExternalChainCapability.BlockBackfill,
            await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken),
            appConfig.Value,
            providerCatalog
        );

    private async Task<string> BuildSignatureAsync(SourceCapabilityRoute route, CancellationToken cancellationToken)
    {
        var jungleBus = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
        return string.Join("|",
            route.PrimarySource,
            string.Join(",", route.FallbackSources),
            jungleBus.BaseUrl ?? string.Empty,
            jungleBus.ApiKey ?? string.Empty,
            jungleBus.BlockSubscriptionId ?? string.Empty);
    }
}
