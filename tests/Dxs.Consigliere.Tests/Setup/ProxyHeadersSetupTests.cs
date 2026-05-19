using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Setup;

/// <summary>
/// wave-A3 S0 — pins the ForwardedHeaders wiring so a future
/// refactor that drops `AddConsigliereForwardedHeaders()` from
/// Startup fails red. Without this middleware Kestrel sees the
/// proxy's loopback IP + the proxy hop's `http` scheme, which
/// silently breaks the cookie Secure flag + the S1 rate limiter.
/// </summary>
public sealed class ProxyHeadersSetupTests
{
    [Fact]
    public void Configures_ForwardedHeaders_For_Proto_And_For()
    {
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddConsigliereForwardedHeaders();

        var provider = services.BuildServiceProvider();
        var options = provider
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>()
            .Value;

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
        // Cleared so the docker bridge / reverse-proxy loopback hop
        // is auto-trusted in the default deployment.
        Assert.Empty(options.KnownNetworks);
        Assert.Empty(options.KnownProxies);
    }
}
