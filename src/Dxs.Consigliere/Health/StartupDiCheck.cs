using Dxs.Consigliere.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;

namespace Dxs.Consigliere.Health;

/// <summary>
/// wave-A3 S2 — `startup`-tagged probe. Asserts the DI graph
/// resolved without throwing. We pick a single load-bearing
/// service (<see cref="IBroadcastService"/>) — if a constructor
/// in its dependency tree threw, the resolve would fail, which
/// is the failure mode this probe exists to catch.
///
/// Resolution is non-blocking on Raven: <c>IBroadcastService</c>
/// holds an <c>IDocumentStore</c> reference, but the store is
/// lazily connected — getting it from the container does not
/// hit Raven. That keeps the startup probe orthogonal to the
/// readiness probe, per the slice contract ("Don't make startup
/// checks blocking on Raven").
/// </summary>
public sealed class StartupDiCheck(IServiceProvider serviceProvider) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = serviceProvider.CreateScope();
            _ = scope.ServiceProvider.GetRequiredService<IBroadcastService>();
            return Task.FromResult(HealthCheckResult.Healthy());
        }
        catch
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("di graph not resolved"));
        }
    }
}
