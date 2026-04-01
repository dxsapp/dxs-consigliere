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
    IExternalChainProviderSettingsAccessor providerSettingsAccessor,
    IOptions<AppConfig> appConfig,
    IExternalChainProviderCatalog providerCatalog,
    ILogger<RealtimeIngestBackgroundTask> logger
) : PeriodicTask(appConfig.Value.BackgroundTasks, logger)
{
    private readonly AppConfig _appConfig = appConfig.Value;
    private readonly ILogger _logger = logger;

    protected override TimeSpan Period => TimeSpan.FromSeconds(2);
    protected override TimeSpan WaitTimeOnError => TimeSpan.FromSeconds(10);

    public override string Name => nameof(RealtimeIngestBackgroundTask);

    protected override async Task RunAsync(CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(cancellationToken);
        var selectedSource = route.PrimarySource;
        var signature = await BuildSignatureAsync(selectedSource, route, cancellationToken);

        _logger.LogInformation(
            "Selected realtime ingest source `{Source}`; primary `{Primary}`; fallbacks {Fallbacks}",
            selectedSource,
            route.PrimarySource,
            route.FallbackSources);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runnerTask = RunSelectedSourceAsync(selectedSource, linkedCancellation.Token);
        var configChanged = false;

        while (!cancellationToken.IsCancellationRequested && !runnerTask.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var currentRoute = await ResolveRouteAsync(cancellationToken);
            var currentSignature = await BuildSignatureAsync(currentRoute.PrimarySource, currentRoute, cancellationToken);

            if (string.Equals(signature, currentSignature, StringComparison.Ordinal))
                continue;

            configChanged = true;
            _logger.LogInformation(
                "Realtime ingest configuration changed from `{OldSignature}` to `{NewSignature}`; recycling runtime task",
                signature,
                currentSignature
            );
            linkedCancellation.Cancel();
        }

        try
        {
            await runnerTask;
        }
        catch (OperationCanceledException) when (configChanged && !cancellationToken.IsCancellationRequested)
        {
            return;
        }
    }

    private Task RunSelectedSourceAsync(string selectedSource, CancellationToken cancellationToken)
    {
        if (string.Equals(selectedSource, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
            return bitailsRunner.RunAsync(cancellationToken);

        if (string.Equals(selectedSource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
            return jungleBusRunner.RunAsync(cancellationToken);

        if (string.Equals(selectedSource, SourceCapabilityRouting.NodeProvider, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Realtime ingest remains delegated to node/ZMQ startup wiring.");
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        throw new InvalidOperationException($"Unsupported realtime ingest source '{selectedSource}'.");
    }

    private async Task<SourceCapabilityRoute> ResolveRouteAsync(CancellationToken cancellationToken)
        => SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken),
            _appConfig,
            providerCatalog
        );

    private async Task<string> BuildSignatureAsync(
        string selectedSource,
        SourceCapabilityRoute route,
        CancellationToken cancellationToken)
    {
        if (string.Equals(selectedSource, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
        {
            var bitails = await providerSettingsAccessor.GetBitailsAsync(cancellationToken);
            return string.Join("|",
                "bitails",
                route.PrimarySource,
                string.Join(",", route.FallbackSources),
                bitails.Transport ?? string.Empty,
                bitails.BaseUrl ?? string.Empty,
                bitails.ApiKey ?? string.Empty,
                bitails.WebsocketBaseUrl ?? string.Empty,
                bitails.ZmqTxUrl ?? string.Empty,
                bitails.ZmqBlockUrl ?? string.Empty);
        }

        if (string.Equals(selectedSource, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
        {
            var jungleBus = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
            return string.Join("|",
                "junglebus",
                route.PrimarySource,
                string.Join(",", route.FallbackSources),
                jungleBus.BaseUrl ?? string.Empty,
                jungleBus.ApiKey ?? string.Empty,
                jungleBus.MempoolSubscriptionId ?? string.Empty);
        }

        return $"{selectedSource}|{route.PrimarySource}|{string.Join(",", route.FallbackSources)}";
    }
}
