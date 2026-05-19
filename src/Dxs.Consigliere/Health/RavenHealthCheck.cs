using Microsoft.Extensions.Diagnostics.HealthChecks;
using Raven.Client.Documents;
using Raven.Client.ServerWide.Operations;

namespace Dxs.Consigliere.Health;

/// <summary>
/// wave-A3 S2 — `ready`-tagged probe. Pings the RavenDB cluster
/// via the lightest server-side operation we have
/// (<c>GetBuildNumberOperation</c>) with a 2-second timeout.
///
/// The check returns <see cref="HealthStatus.Unhealthy"/> when
/// the cluster does not answer in time; the description is
/// deliberately terse ("raven not reachable") so we don't leak
/// internal hostnames + ports through an anonymous probe.
/// </summary>
public sealed class RavenHealthCheck(IDocumentStore documentStore) : IHealthCheck
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(Timeout);
        try
        {
            await documentStore.Maintenance.Server.SendAsync(new GetBuildNumberOperation(), cts.Token);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("raven not reachable (timeout)");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("raven not reachable");
        }
    }
}
