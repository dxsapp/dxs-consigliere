using System.Net;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dxs.Consigliere.Tests.Health;

/// <summary>
/// wave-A3 S2 — pins the verdict matrix the readiness probe
/// produces against a canned <see cref="HttpClient"/>. Network
/// is mocked entirely via a stub <see cref="HttpMessageHandler"/>;
/// nothing leaves the test process.
///
/// S2-audit M1 update: the check now sources its target list
/// from <see cref="IAdminProviderConfigService.GetEffectiveSourcesConfigAsync"/>
/// (Raven-backed overrides applied), not from the static
/// IOptionsMonitor. Tests stub that interface accordingly and
/// add coverage for: (a) disabled providers are skipped, (b) a
/// throwing service degrades the probe rather than 503-ing.
/// </summary>
public sealed class ProviderReachabilityCheckTests
{
    [Fact]
    public async Task All_targets_reachable_returns_Healthy()
    {
        var check = BuildCheck(AllEnabled(), (_, _) => Reply(HttpStatusCode.OK));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Result_is_cached_so_a_flood_collapses_to_one_probe_set()
    {
        // S2/S3-audit L3: `/health/ready` is anonymous +
        // rate-limit-exempt. Two back-to-back checks (well within
        // the 5s TTL) must fire the outbound probe-set ONCE, not
        // twice — otherwise a flood amplifies into 3 outbound HEADs
        // per hit.
        var probeCalls = 0;
        var check = BuildCheck(AllEnabled(), (_, _) =>
        {
            Interlocked.Increment(ref probeCalls);
            return Reply(HttpStatusCode.OK);
        });

        var first = await check.CheckHealthAsync(new HealthCheckContext());
        var second = await check.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, first.Status);
        Assert.Equal(HealthStatus.Healthy, second.Status);
        // 3 enabled targets × ONE probe-set (second call served
        // from cache).
        Assert.Equal(3, probeCalls);
    }

    [Fact]
    public async Task Concurrent_floods_single_flight_to_one_probe_set()
    {
        // The single-flight gate must collapse concurrent cold-cache
        // callers to one probe-set too.
        var probeCalls = 0;
        var check = BuildCheck(AllEnabled(), (_, _) =>
        {
            Interlocked.Increment(ref probeCalls);
            return Reply(HttpStatusCode.OK);
        });

        await Task.WhenAll(Enumerable.Range(0, 10)
            .Select(_ => check.CheckHealthAsync(new HealthCheckContext())));

        Assert.Equal(3, probeCalls);
    }

    [Fact]
    public async Task One_target_down_returns_Degraded_and_lists_it()
    {
        var check = BuildCheck(AllEnabled(), (url, _) =>
            url.Host.Contains("bitails", StringComparison.OrdinalIgnoreCase)
                ? throw new HttpRequestException("connection refused")
                : Reply(HttpStatusCode.OK));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Contains("bitails", result.Description, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task All_targets_down_returns_Unhealthy()
    {
        var check = BuildCheck(AllEnabled(), (_, _) => throw new HttpRequestException("no route"));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Five_hundred_response_counts_as_unreachable()
    {
        // 5xx == "host up but unhappy"; for readiness we treat
        // that the same as connection failure — the broadcast
        // path would surface it identically.
        var check = BuildCheck(AllEnabled(), (_, _) => Reply(HttpStatusCode.InternalServerError));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Four_hundred_response_counts_as_reachable()
    {
        var check = BuildCheck(AllEnabled(), (_, _) => Reply(HttpStatusCode.NotFound));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Disabled_providers_are_not_probed()
    {
        // S2-audit M1: the operator can flip a provider off
        // through the admin UI; the readiness probe must
        // honour that and not flag an unreachable but
        // intentionally-off provider as down.
        var config = new ConsigliereSourcesConfig
        {
            Providers = new SourceProvidersConfig
            {
                Bitails = new BitailsSourceConfig
                {
                    Enabled = true,
                    Connection = new BitailsSourceConnectionConfig { BaseUrl = "https://bitails.test/" }
                },
                Whatsonchain = new WhatsOnChainSourceConfig
                {
                    Enabled = false, // explicitly off
                    Connection = new HttpApiSourceConnectionConfig { BaseUrl = "https://whatsonchain.test/" }
                },
                JungleBus = new JungleBusSourceConfig { Enabled = false },
            }
        };
        var seen = new List<string>();
        var check = BuildCheck(config, (url, _) =>
        {
            seen.Add(url.Host);
            return Reply(HttpStatusCode.OK);
        });
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
        Assert.Single(seen);
        Assert.Contains("bitails", seen[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task No_enabled_providers_returns_Healthy()
    {
        var config = new ConsigliereSourcesConfig
        {
            Providers = new SourceProvidersConfig
            {
                Bitails = new BitailsSourceConfig { Enabled = false },
                Whatsonchain = new WhatsOnChainSourceConfig { Enabled = false },
                JungleBus = new JungleBusSourceConfig { Enabled = false },
            }
        };
        var check = BuildCheck(config, (_, _) =>
            throw new InvalidOperationException("must not be called"));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Effective_config_throws_returns_Degraded_with_terse_message()
    {
        // S2-audit M1 recommended fix: if effective-config
        // retrieval fails (typically because Raven is down),
        // the provider probe must NOT also report Unhealthy —
        // the raven check carries that signal. We return
        // Degraded so the overall readiness still fails (raven
        // → Unhealthy), without doubling the same root cause.
        var service = new ThrowingProviderConfigService();
        var factory = new StubHttpClientFactory(new StubHttpHandler((_, _) =>
            throw new InvalidOperationException("must not be called")));
        var check = new ProviderReachabilityCheck(service, factory);

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal("config unavailable", result.Description);
    }

    private static ProviderReachabilityCheck BuildCheck(
        ConsigliereSourcesConfig config,
        Func<Uri, CancellationToken, HttpResponseMessage> respond)
    {
        var service = new StubProviderConfigService(config);
        var handler = new StubHttpHandler((req, ct) => respond(req.RequestUri!, ct));
        return new ProviderReachabilityCheck(service, new StubHttpClientFactory(handler));
    }

    private static ConsigliereSourcesConfig AllEnabled() => new()
    {
        Providers = new SourceProvidersConfig
        {
            Bitails = new BitailsSourceConfig
            {
                Enabled = true,
                Connection = new BitailsSourceConnectionConfig { BaseUrl = "https://bitails.test/" }
            },
            Whatsonchain = new WhatsOnChainSourceConfig
            {
                Enabled = true,
                Connection = new HttpApiSourceConnectionConfig { BaseUrl = "https://whatsonchain.test/" }
            },
            JungleBus = new JungleBusSourceConfig
            {
                Enabled = true,
                Connection = new JungleBusSourceConnectionConfig { BaseUrl = "https://junglebus.test/" }
            },
        }
    };

    private static HttpResponseMessage Reply(HttpStatusCode code) => new(code);

    private sealed class StubProviderConfigService(ConsigliereSourcesConfig config) : IAdminProviderConfigService
    {
        public Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(config);

        public Task<JungleBusProviderRuntimeSnapshot> GetEffectiveJungleBusAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProvidersResponse> GetProvidersAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProviderConfigMutationResult> ApplyProviderConfigAsync(
            AdminProviderConfigUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProvidersResponse> ResetProviderConfigAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ThrowingProviderConfigService : IAdminProviderConfigService
    {
        public Task<ConsigliereSourcesConfig> GetEffectiveSourcesConfigAsync(CancellationToken cancellationToken = default)
            => Task.FromException<ConsigliereSourcesConfig>(new InvalidOperationException("raven down"));

        public Task<JungleBusProviderRuntimeSnapshot> GetEffectiveJungleBusAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProvidersResponse> GetProvidersAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProviderConfigMutationResult> ApplyProviderConfigAsync(
            AdminProviderConfigUpdateRequest request, string updatedBy, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<AdminProvidersResponse> ResetProviderConfigAsync(CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHttpHandler(Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try { return Task.FromResult(respond(request, cancellationToken)); }
            catch (Exception ex) { return Task.FromException<HttpResponseMessage>(ex); }
        }
    }
}
