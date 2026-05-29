using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dxs.Consigliere.Health;

/// <summary>
/// wave-A3 S2 — `ready`-tagged probe. Touches every ENABLED
/// external chain provider in the EFFECTIVE source config
/// (Raven-backed overrides applied — same view the routing
/// layer uses) with a HEAD request + 5-second per-target
/// timeout.
///
/// S2-audit M1 fix: previous revision read
/// `IOptionsMonitor&lt;ConsigliereSourcesConfig&gt;`, which sees
/// only the appsettings + env layer and misses the operator's
/// Raven-stored provider overrides. The probe was validating
/// the wrong URLs / wrong enabled set, which made
/// `/health/ready` lie any time the operator had customised a
/// provider through the admin UI.
///
/// Semantics:
///   - all enabled targets reachable → Healthy
///   - at least one reachable → Degraded (lists the down sources)
///   - no targets reachable → Unhealthy
///   - effective config retrieval throws (Raven down) →
///     Degraded with "config unavailable" — the raven check
///     carries the real Unhealthy signal so we don't double
///     count the same failure.
///
/// Reachability is loose on purpose: any HTTP response with
/// status &lt; 500 counts as "the host answered." We only count
/// connection failure / 5xx / timeout as a miss — that's what
/// the broadcast + ingest paths actually surface.
///
/// Descriptions never include URLs or status codes — this
/// endpoint is anonymous (k8s probes can't ship cookies).
///
/// S2/S3-audit L3: `/health/ready` is anonymous + rate-limit-
/// exempt, and each hit fires one outbound HEAD per enabled
/// provider. To stop that becoming an amplification / self-DoS
/// vector, the probe result is cached for <see cref="CacheTtl"/>
/// behind a single-flight gate — a flood collapses to at most
/// one provider-probe-set per window. The check is registered as
/// a singleton (see <c>HealthChecksSetup</c>) so the cache
/// persists across requests.
/// </summary>
public sealed class ProviderReachabilityCheck(
    IAdminProviderConfigService providerConfigService,
    IHttpClientFactory httpClientFactory) : IHealthCheck
{
    private static readonly TimeSpan PerTargetTimeout = TimeSpan.FromSeconds(5);
    internal static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(5);
    internal const string HttpClientName = "consigliere-health-provider";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private HealthCheckResult _cached;
    private DateTimeOffset _cachedAtUtc = DateTimeOffset.MinValue;
    private bool _hasCached;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (TryGetFresh(out var cached)) return cached;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Double-check: a concurrent caller may have just
            // refreshed the cache while we waited on the gate.
            if (TryGetFresh(out cached)) return cached;

            var result = await ProbeAllAsync(cancellationToken);
            _cached = result;
            _cachedAtUtc = DateTimeOffset.UtcNow;
            _hasCached = true;
            return result;
        }
        finally
        {
            _gate.Release();
        }
    }

    private bool TryGetFresh(out HealthCheckResult result)
    {
        if (_hasCached && DateTimeOffset.UtcNow - _cachedAtUtc < CacheTtl)
        {
            result = _cached;
            return true;
        }
        result = default;
        return false;
    }

    private async Task<HealthCheckResult> ProbeAllAsync(CancellationToken cancellationToken)
    {
        ConsigliereSourcesConfig effective;
        try
        {
            effective = await providerConfigService.GetEffectiveSourcesConfigAsync(cancellationToken);
        }
        catch
        {
            // Don't double-count the same failure. The raven
            // health check is the authoritative signal that
            // the persistence layer is down.
            return HealthCheckResult.Degraded("config unavailable");
        }

        var targets = CollectEnabledTargets(effective.Providers);
        if (targets.Count == 0)
        {
            // No enabled external providers in the effective
            // config — the setup wizard guarantees at least
            // one in normal operation, so this typically only
            // hits in a fresh-install or all-disabled state.
            return HealthCheckResult.Healthy("no enabled providers");
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

    private static List<(string Name, string Url)> CollectEnabledTargets(SourceProvidersConfig providers)
    {
        var list = new List<(string Name, string Url)>(3);
        AddIfEnabled(list, "bitails", providers.Bitails.Enabled, providers.Bitails.Connection.BaseUrl);
        AddIfEnabled(list, "whatsonchain", providers.Whatsonchain.Enabled, providers.Whatsonchain.Connection.BaseUrl);
        AddIfEnabled(list, "junglebus", providers.JungleBus.Enabled, providers.JungleBus.Connection.BaseUrl);
        return list;
    }

    private static void AddIfEnabled(List<(string, string)> list, string name, bool enabled, string baseUrl)
    {
        if (!enabled) return;
        if (string.IsNullOrWhiteSpace(baseUrl)) return;
        list.Add((name, baseUrl));
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
