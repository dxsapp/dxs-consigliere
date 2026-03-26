using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public static class VNextStartupDiagnostics
{
    public static string[] Describe(ConsigliereSourcesConfig sources, ConsigliereStorageConfig storage)
    {
        var enabledProviders = GetEnabledProviders(sources);
        var payloadStore = DescribePayloadStore(storage);

        return
        [
            $"VNext routing mode: {sources.Routing.PreferredMode ?? "(unset)"}",
            $"VNext primary source: {sources.Routing.PrimarySource ?? "(unset)"}",
            $"VNext fallback sources: {FormatList(sources.Routing.FallbackSources)}",
            $"VNext verification source: {sources.Routing.VerificationSource ?? "(unset)"}",
            $"VNext enabled providers: {FormatList(enabledProviders)}",
            $"VNext raw payload storage: {payloadStore}"
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

        providers.Add($"{providerName}{capabilities}");
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
    IOptions<ConsigliereSourcesConfig> sources,
    IOptions<ConsigliereStorageConfig> storage
) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var line in VNextStartupDiagnostics.Describe(sources.Value, storage.Value))
            logger.LogInformation("{StartupDiagnostic}", line);

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
