using System.Net;
using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Health;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Health;

/// <summary>
/// wave-A3 S2 — pins the three-state (Healthy / Degraded /
/// Unhealthy) verdict the readiness probe produces against a
/// canned <see cref="HttpClient"/>. Network is mocked entirely
/// via a stub <see cref="HttpMessageHandler"/>; nothing leaves
/// the test process.
/// </summary>
public sealed class ProviderReachabilityCheckTests
{
    [Fact]
    public async Task All_targets_reachable_returns_Healthy()
    {
        var check = BuildCheck((_, _) => Reply(HttpStatusCode.OK));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task One_target_down_returns_Degraded_and_lists_it()
    {
        var check = BuildCheck((url, _) =>
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
        var check = BuildCheck((_, _) => throw new HttpRequestException("no route"));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Five_hundred_response_counts_as_unreachable()
    {
        // 5xx == "host up but unhappy"; for readiness we treat
        // that the same as connection failure — the broadcast
        // path would surface it identically.
        var check = BuildCheck((_, _) => Reply(HttpStatusCode.InternalServerError));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
    }

    [Fact]
    public async Task Four_hundred_response_counts_as_reachable()
    {
        // The host answered with HTTP semantics — that's enough
        // for readiness. The routing layer would log + skip on
        // 4xx; the probe doesn't care.
        var check = BuildCheck((_, _) => Reply(HttpStatusCode.NotFound));
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task No_configured_providers_returns_Healthy()
    {
        var monitor = new StubOptionsMonitor(new ConsigliereSourcesConfig
        {
            Providers = new SourceProvidersConfig
            {
                Bitails = new BitailsSourceConfig(),
                Whatsonchain = new WhatsOnChainSourceConfig(),
                JungleBus = new JungleBusSourceConfig(),
            }
        });
        var factory = new StubHttpClientFactory(new StubHttpHandler((_, _) =>
            throw new InvalidOperationException("must not be called")));
        var check = new ProviderReachabilityCheck(monitor, factory);

        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    private static ProviderReachabilityCheck BuildCheck(
        Func<Uri, CancellationToken, HttpResponseMessage> respond)
    {
        var monitor = new StubOptionsMonitor(new ConsigliereSourcesConfig
        {
            Providers = new SourceProvidersConfig
            {
                Bitails = new BitailsSourceConfig
                {
                    Connection = new BitailsSourceConnectionConfig { BaseUrl = "https://bitails.test/" }
                },
                Whatsonchain = new WhatsOnChainSourceConfig
                {
                    Connection = new HttpApiSourceConnectionConfig { BaseUrl = "https://whatsonchain.test/" }
                },
                JungleBus = new JungleBusSourceConfig
                {
                    Connection = new JungleBusSourceConnectionConfig { BaseUrl = "https://junglebus.test/" }
                },
            }
        });
        var handler = new StubHttpHandler((req, ct) => respond(req.RequestUri!, ct));
        return new ProviderReachabilityCheck(monitor, new StubHttpClientFactory(handler));
    }

    private static HttpResponseMessage Reply(HttpStatusCode code) => new(code);

    private sealed class StubOptionsMonitor(ConsigliereSourcesConfig value) : IOptionsMonitor<ConsigliereSourcesConfig>
    {
        public ConsigliereSourcesConfig CurrentValue { get; } = value;
        public ConsigliereSourcesConfig Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ConsigliereSourcesConfig, string?> listener) => null;
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
