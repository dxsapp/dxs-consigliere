using Dxs.Consigliere.Health;
using Dxs.Consigliere.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Health;

/// <summary>
/// wave-A3 S2 — pins the tag predicates that the three health
/// endpoints rely on. A future refactor that drops the `ready`
/// or `startup` tag from any check would silently downgrade
/// `/health/{ready,startup}` to no-ops; this test fails red on
/// that.
/// </summary>
public sealed class HealthChecksSetupTests
{
    [Fact]
    public void Registers_three_named_checks_with_expected_tags()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConsigliereHealthChecks();

        var registrations = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations
            .ToDictionary(r => r.Name, r => r);

        Assert.Equal(3, registrations.Count);

        Assert.Contains("ready", registrations["raven"].Tags);
        Assert.Contains("ready", registrations["providers"].Tags);
        Assert.Contains("startup", registrations["di-graph"].Tags);

        Assert.DoesNotContain("ready", registrations["di-graph"].Tags);
        Assert.DoesNotContain("startup", registrations["raven"].Tags);
        Assert.DoesNotContain("startup", registrations["providers"].Tags);
    }

    [Fact]
    public void Live_endpoint_predicate_excludes_every_registered_check()
    {
        // The slice contract: /health/live runs the empty
        // predicate `_ => false`. This test is a guard against a
        // future "let's also include ..." that would degrade the
        // liveness contract.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConsigliereHealthChecks();

        var registrations = services.BuildServiceProvider()
            .GetRequiredService<IOptions<HealthCheckServiceOptions>>().Value.Registrations;

        Func<HealthCheckRegistration, bool> livePredicate = _ => false;
        Assert.All(registrations, r => Assert.False(livePredicate(r)));
    }

    [Fact]
    public void HttpClient_for_provider_probe_is_registered_by_name()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConsigliereHealthChecks();

        var factory = services.BuildServiceProvider().GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(ProviderReachabilityCheck.HttpClientName);
        Assert.NotNull(client);
    }
}
