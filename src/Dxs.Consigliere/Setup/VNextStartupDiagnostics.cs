#nullable enable

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public static class VNextStartupDiagnostics
{
    public static string[] Describe(
        ConsigliereSourcesConfig sources,
        ConsigliereStorageConfig storage,
        ConsigliereCacheConfig cache,
        string? cutoverMode = null
    )
    {
        var enabledProviders = GetEnabledProviders(sources);
        var payloadStore = DescribePayloadStore(storage);
        var cacheStore = DescribeCache(cache);

        return
        [
            $"VNext cutover mode: {cutoverMode ?? VNextCutoverMode.Legacy}",
            $"VNext routing mode: {sources.Routing.PreferredMode ?? "(unset)"}",
            $"VNext primary source: {sources.Routing.PrimarySource ?? "(unset)"}",
            $"VNext fallback sources: {FormatList(sources.Routing.FallbackSources)}",
            $"VNext verification source: {sources.Routing.VerificationSource ?? "(unset)"}",
            $"VNext historical address source: {sources.Capabilities.HistoricalAddressScan.Source ?? "(unset)"}",
            $"VNext historical token source: {sources.Capabilities.HistoricalTokenScan.Source ?? "(unset)"}",
            $"VNext enabled providers: {FormatList(enabledProviders)}",
            $"VNext raw payload storage: {payloadStore}",
            $"VNext projection cache: {cacheStore}"
        ];
    }

    private static string[] GetEnabledProviders(ConsigliereSourcesConfig sources)
    {
        var result = new List<string>();

        AddIfEnabled(result, "node", sources.Providers.Node);
        AddIfEnabled(result, "junglebus", sources.Providers.JungleBus);
        AddIfEnabled(result, "bitails", sources.Providers.Bitails);
        AddIfEnabled(result, "whatsonchain", sources.Providers.Whatsonchain);

        return result.ToArray();
    }

    private static void AddIfEnabled(List<string> providers, string providerName, SourceProviderConfig config)
    {
        if (!config.Enabled)
            return;

        var capabilities = config.EnabledCapabilities?.Length > 0
            ? $" ({string.Join(", ", config.EnabledCapabilities)})"
            : string.Empty;

        var transport = DescribeTransport(providerName, config);
        providers.Add($"{providerName}{transport}{capabilities}");
    }

    private static string DescribeTransport(string providerName, SourceProviderConfig config)
    {
        if (!string.Equals(providerName, "bitails", StringComparison.OrdinalIgnoreCase) ||
            config is not BitailsSourceConfig bitails ||
            string.IsNullOrWhiteSpace(bitails.Connection.Transport))
        {
            return string.Empty;
        }

        return $"[{bitails.Connection.Transport}]";
    }

    private static string DescribePayloadStore(ConsigliereStorageConfig storage)
    {
        var payloads = storage.RawTransactionPayloads;
        if (!payloads.Enabled)
            return "disabled";

        return payloads.Provider?.ToLowerInvariant() switch
        {
            "raven" => $"raven/{payloads.Location.Database ?? "(default-db)"}/{payloads.Location.Collection}",
            "filesystem" => $"filesystem/{payloads.Location.RootPath}",
            "s3" => $"s3/{payloads.Location.Bucket}/{payloads.Location.Prefix ?? string.Empty}".TrimEnd('/'),
            _ => payloads.Provider ?? "(unset)"
        };
    }

    private static string DescribeCache(ConsigliereCacheConfig cache)
    {
        if (!cache.Enabled || string.Equals(cache.Backend, "disabled", StringComparison.OrdinalIgnoreCase))
            return "disabled";

        return cache.Backend?.ToLowerInvariant() switch
        {
            "memory" => $"memory/{cache.MaxEntries}",
            "azos" => $"azos/{cache.Azos?.TableName ?? "projection-read-cache"}/{cache.MaxEntries}",
            _ => cache.Backend ?? "(unset)"
        };
    }

    private static string FormatList(IEnumerable<string> values)
    {
        var items = values?.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray() ?? [];
        return items.Length == 0
            ? "(none)"
            : string.Join(", ", items);
    }
}

public sealed class VNextStartupDiagnosticsHostedService(
    ILogger<VNextStartupDiagnosticsHostedService> logger,
    IAdminProviderConfigService providerConfigService,
    IOptions<ConsigliereStorageConfig> storage,
    IOptions<ConsigliereCacheConfig> cache,
    IOptions<AppConfig> appConfig
) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var sources = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        foreach (var line in VNextStartupDiagnostics.Describe(
                     sources,
                     storage.Value,
                     cache.Value,
                     appConfig.Value.VNextRuntime.CutoverMode))
            logger.LogInformation("{StartupDiagnostic}", line);

        return;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
