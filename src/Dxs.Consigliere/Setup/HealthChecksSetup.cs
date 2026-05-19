using Dxs.Consigliere.Health;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Dxs.Consigliere.Setup;

public static class HealthChecksSetup
{
    /// <summary>
    /// wave-A3 S2 — registers the three named probes plus the
    /// shared `HttpClient` the provider-reachability probe uses.
    /// Tags follow the slice contract: `live` has no checks (it
    /// only proves the process is listening), `ready` covers
    /// dependencies (raven + providers), `startup` covers the
    /// DI graph.
    /// </summary>
    public static IServiceCollection AddConsigliereHealthChecks(this IServiceCollection services)
    {
        services.AddHttpClient(ProviderReachabilityCheck.HttpClientName);
        services.AddHealthChecks()
            .AddCheck<RavenHealthCheck>(
                name: "raven",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"])
            .AddCheck<ProviderReachabilityCheck>(
                name: "providers",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["ready"])
            .AddCheck<StartupDiCheck>(
                name: "di-graph",
                failureStatus: HealthStatus.Unhealthy,
                tags: ["startup"]);
        return services;
    }

    public static IEndpointRouteBuilder MapConsigliereHealthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // `live` — the process is up + listening. No checks run.
        // Orchestrators use this to distinguish "restart me" from
        // "give me a second to warm up."
        endpoints.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false,
            ResponseWriter = HealthResponseWriter.Write,
        }).AllowAnonymous();

        // `ready` — every `ready`-tagged check passes (Raven +
        // providers).
        endpoints.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("ready"),
            ResponseWriter = HealthResponseWriter.Write,
        }).AllowAnonymous();

        // `startup` — DI graph resolved without throwing.
        endpoints.MapHealthChecks("/health/startup", new HealthCheckOptions
        {
            Predicate = c => c.Tags.Contains("startup"),
            ResponseWriter = HealthResponseWriter.Write,
        }).AllowAnonymous();

        return endpoints;
    }
}
