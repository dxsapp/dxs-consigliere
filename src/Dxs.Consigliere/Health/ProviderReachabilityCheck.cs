using Dxs.Consigliere.Configs;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Health;

/// <summary>
/// wave-A3 S2 — `ready`-tagged probe. Touches every configured
/// external chain provider (Bitails, WhatsOnChain, JungleBus)
/// with a HEAD request + 5-second per-target timeout.
///
/// Semantics:
///   - all targets reachable → Healthy
///   - at least one target reachable → Degraded
///     (the routing layer can fall back to a sibling)
///   - no targets reachable → Unhealthy
///
/// Reachability is loose on purpose: any HTTP response — even
/// 4xx — counts as "the host answered." We only count
/// connection failure / 5xx / timeout as a miss, because that's
/// what the broadcast + ingest paths actually surface.
///
/// Descriptions never include URLs or status codes — this
/// endpoint is anonymous (k8s probes can't ship cookies).
/// </summary>
public sealed class ProviderReachabilityCheck(
    IOptionsMonitor<ConsigliereSourcesConfig> sourcesOptions,
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private static readonly TimeSpan PerTargetTimeout = TimeSpan.FromSeconds(5);
    internal const string HttpClientName = "consigliere-health-provider";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var providers = sourcesOptions.CurrentValue.Providers;
        var targets = new List<(string Name, string Url)>(3);
        if (!string.IsNullOrWhiteSpace(providers.Bitails.Connection.BaseUrl))
            targets.Add(("bitails", providers.Bitails.Connection.BaseUrl));
        if (!string.IsNullOrWhiteSpace(providers.Whatsonchain.Connection.BaseUrl))
            targets.Add(("whatsonchain", providers.Whatsonchain.Connection.BaseUrl));
        if (!string.IsNullOrWhiteSpace(providers.JungleBus.Connection.BaseUrl))
            targets.Add(("junglebus", providers.JungleBus.Connection.BaseUrl));

        if (targets.Count == 0)
        {
            // No providers configured = no external dependencies
            // claimed; the readiness contract is satisfied by
            // default. The setup wizard guarantees at least one.
            return HealthCheckResult.Healthy("no providers configured");
        }

        var client = httpClientFactory.CreateClient(HttpClientName);
        var probes = await Task.WhenAll(targets.Select(t => ProbeAsync(client, t, cancellationToken)));

        var reachable = probes.Count(p => p.Reachable);
        var down = probes.Where(p => !p.Reachable).Select(p => p.Name).ToArray();

        if (reachable == targets.Count)
            return HealthCheckResult.Healthy();
        if (reachable > 0)
            return HealthCheckResult.Degraded($"degraded: {string.Join(",", down)} unreachable");
        return HealthCheckResult.Unhealthy("all providers unreachable");
    }

    private static async Task<(string Name, bool Reachable)> ProbeAsync(
        HttpClient client,
        (string Name, string Url) target,
        CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(PerTargetTimeout);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, target.Url);
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            // Any HTTP response — including 4xx — proves the host
            // answered. 5xx means the host is up but unhappy: we
            // count that as a miss for readiness purposes.
            return (target.Name, (int)response.StatusCode < 500);
        }
        catch
        {
            return (target.Name, false);
        }
    }
}
