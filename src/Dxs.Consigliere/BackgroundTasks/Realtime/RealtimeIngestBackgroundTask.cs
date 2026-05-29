using Dxs.Common.BackgroundTasks;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Services.Impl;
using Dxs.Infrastructure.Common;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.BackgroundTasks.Realtime;

/// <summary>
/// Drives the external realtime ingest runners.
///
/// thin-node-primary-source S3 (Product Decision 2 / Core Rules 3-4):
/// "Primary" is the canonical attribution/quorum anchor, NOT an
/// exclusive switch. Making <c>p2p</c> the realtime primary must NOT
/// silence the external runners — every enabled external realtime
/// provider observes concurrently (redundant, "who's fastest"), so a
/// cold P2P pool (zero peers) never stalls ingest. This task therefore
/// runs EVERY enabled external realtime runner (Bitails + JungleBus) in
/// parallel, regardless of which provider the route resolves as primary.
///
/// The in-house thin node (<c>p2p</c>) runs as its own always-on hosted
/// service (<see cref="Dxs.Consigliere.Services.P2p.P2pMempoolIngestRunner"/>
/// wired in <c>BsvP2pSetup</c>, gated only on <c>BsvP2pConfig.Enabled</c>)
/// and is intentionally NOT scheduled here. The full <c>node</c> RPC/ZMQ
/// path is bootstrapped by <c>AppInitBackgroundTask</c>. Both are
/// independent of this task.
///
/// The journal is already source-aware (<c>SeenBySources</c> accumulates)
/// and dedupes by txid (bsv-mempool-observer-wave), so concurrent appends
/// from multiple runners for the same txid are safe — this task does NOT
/// touch the journal write contract or dedup.
/// </summary>
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
        var sources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
        var route = ResolveRoute(sources);
        var enabledRunners = ResolveEnabledExternalRunners(sources);
        var signature = await BuildSignatureAsync(enabledRunners, route, cancellationToken);

        _logger.LogInformation(
            "Realtime ingest: running {RunnerCount} external runner(s) [{Runners}] concurrently; primary `{Primary}` (attribution/quorum anchor); fallbacks {Fallbacks}",
            enabledRunners.Count,
            string.Join(", ", enabledRunners),
            route.PrimarySource,
            route.FallbackSources);

        using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runnerTask = RunEnabledRunnersAsync(enabledRunners, linkedCancellation.Token);
        var configChanged = false;

        while (!cancellationToken.IsCancellationRequested && !runnerTask.IsCompleted)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
            var currentSources = await runtimeSourcePolicyService.GetEffectiveSourcesConfigAsync(cancellationToken);
            var currentRoute = ResolveRoute(currentSources);
            var currentRunners = ResolveEnabledExternalRunners(currentSources);
            var currentSignature = await BuildSignatureAsync(currentRunners, currentRoute, cancellationToken);

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

    /// <summary>
    /// Run every enabled external realtime runner concurrently. They are
    /// peers — none is gated on being the routed primary, and none is
    /// gated on P2P health (the rejected "failover" design; the operator
    /// chose always-on redundancy). If a single runner faults, the linked
    /// token is cancelled so the others recycle together via the periodic
    /// re-entry of <see cref="RunAsync"/> (preserving the pre-S3 fault
    /// semantics for the previously-single primary runner).
    /// </summary>
    private Task RunEnabledRunnersAsync(IReadOnlyList<string> enabledRunners, CancellationToken cancellationToken)
    {
        if (enabledRunners.Count == 0)
        {
            _logger.LogInformation(
                "No external realtime runner enabled; realtime ingest relies on the always-on P2P observer / node wiring.");
            return Task.Delay(Timeout.Infinite, cancellationToken);
        }

        var tasks = new List<Task>(enabledRunners.Count);
        foreach (var runner in enabledRunners)
        {
            if (string.Equals(runner, ExternalChainProviderName.Bitails, StringComparison.OrdinalIgnoreCase))
                tasks.Add(bitailsRunner.RunAsync(cancellationToken));
            else if (string.Equals(runner, ExternalChainProviderName.JungleBus, StringComparison.OrdinalIgnoreCase))
                tasks.Add(jungleBusRunner.RunAsync(cancellationToken));
        }

        return Task.WhenAll(tasks);
    }

    /// <summary>
    /// The set of external providers whose runners this task drives:
    /// providers that are enabled AND declare the RealtimeIngest
    /// capability AND have an in-process realtime runner here
    /// (Bitails, JungleBus). Deliberately NOT hard-coded to a fixed pair
    /// — driven by the effective config so disabling a provider drops its
    /// runner. <c>p2p</c> and <c>node</c> are excluded: they run via their
    /// own always-on wiring, not as runners on this task.
    /// </summary>
    internal static IReadOnlyList<string> ResolveEnabledExternalRunners(ConsigliereSourcesConfig sources)
    {
        var runners = new List<string>(2);

        if (HasRealtimeIngest(sources.Providers.Bitails))
            runners.Add(ExternalChainProviderName.Bitails);

        if (HasRealtimeIngest(sources.Providers.JungleBus))
            runners.Add(ExternalChainProviderName.JungleBus);

        return runners;
    }

    private static bool HasRealtimeIngest(SourceProviderConfig config)
        => config is { Enabled: true } &&
            config.EnabledCapabilities.Contains(ExternalChainCapability.RealtimeIngest, StringComparer.OrdinalIgnoreCase);

    private SourceCapabilityRoute ResolveRoute(ConsigliereSourcesConfig sources)
        => SourceCapabilityRouting.Resolve(
            ExternalChainCapability.RealtimeIngest,
            sources,
            _appConfig,
            providerCatalog
        );

    /// <summary>
    /// Recycle signature: the set of enabled external runners + the
    /// resolved route + the per-provider connection settings of every
    /// enabled runner. A change to which runners are enabled, to the
    /// primary/fallback route, or to a runner's transport/credentials
    /// recycles the task so the new configuration takes effect.
    /// </summary>
    private async Task<string> BuildSignatureAsync(
        IReadOnlyList<string> enabledRunners,
        SourceCapabilityRoute route,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>
        {
            "runners:" + string.Join(",", enabledRunners),
            "primary:" + route.PrimarySource,
            "fallbacks:" + string.Join(",", route.FallbackSources)
        };

        if (enabledRunners.Contains(ExternalChainProviderName.Bitails, StringComparer.OrdinalIgnoreCase))
        {
            var bitails = await providerSettingsAccessor.GetBitailsAsync(cancellationToken);
            parts.Add(string.Join("|",
                "bitails",
                bitails.Transport ?? string.Empty,
                bitails.BaseUrl ?? string.Empty,
                bitails.ApiKey ?? string.Empty,
                bitails.WebsocketBaseUrl ?? string.Empty,
                bitails.ZmqTxUrl ?? string.Empty,
                bitails.ZmqBlockUrl ?? string.Empty));
        }

        if (enabledRunners.Contains(ExternalChainProviderName.JungleBus, StringComparer.OrdinalIgnoreCase))
        {
            var jungleBus = await providerSettingsAccessor.GetJungleBusAsync(cancellationToken);
            parts.Add(string.Join("|",
                "junglebus",
                jungleBus.BaseUrl ?? string.Empty,
                jungleBus.ApiKey ?? string.Empty,
                jungleBus.MempoolSubscriptionId ?? string.Empty));
        }

        return string.Join("||", parts);
    }
}
