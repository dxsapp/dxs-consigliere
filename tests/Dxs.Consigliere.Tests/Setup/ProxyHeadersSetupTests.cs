using Dxs.Consigliere.Setup;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Setup;

/// <summary>
/// wave-A3 S0 — pins the ForwardedHeaders wiring so a future
/// refactor that drops `AddConsigliereForwardedHeaders()` from
/// Startup fails red. Without this middleware Kestrel sees the
/// proxy's loopback IP + the proxy hop's `http` scheme, which
/// silently breaks the cookie Secure flag + the S1 rate limiter.
///
/// S0/S1-audit L1+L2: also pins the operator-tunable trust list
/// (string CIDRs / IPs that bind from env, unlike the framework's
/// IPAddress-typed lists).
/// </summary>
public sealed class ProxyHeadersSetupTests
{
    [Fact]
    public void Configures_ForwardedHeaders_For_Proto_And_For()
    {
        var options = BuildOptions(new Dictionary<string, string?>());

        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedFor));
        Assert.True(options.ForwardedHeaders.HasFlag(ForwardedHeaders.XForwardedProto));
    }

    [Fact]
    public void No_trust_list_keeps_permissive_default()
    {
        // No config → both lists empty → middleware consumes
        // X-Forwarded-* from any immediate caller (safe behind
        // Caddy + unpublished Kestrel). The documented fallback.
        var options = BuildOptions(new Dictionary<string, string?>());
        Assert.Empty(options.KnownNetworks);
        Assert.Empty(options.KnownProxies);
    }

    [Fact]
    public void Configured_trust_list_populates_known_networks_and_proxies()
    {
        // S0/S1-audit L1+L2: the working knob. STRING values bind
        // from env-style config keys (the whole point — IPAddress
        // doesn't).
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["Consigliere:ForwardedHeaders:KnownNetworks:0"] = "172.16.0.0/12",
            ["Consigliere:ForwardedHeaders:KnownProxies:0"] = "10.0.0.5",
        });

        Assert.Single(options.KnownNetworks);
        Assert.Equal(12, options.KnownNetworks[0].PrefixLength);
        Assert.Single(options.KnownProxies);
        Assert.Equal("10.0.0.5", options.KnownProxies[0].ToString());
    }

    [Fact]
    public void Malformed_trust_entry_is_skipped_not_fatal()
    {
        // One bad CIDR must not take down host startup; the good
        // entry still lands.
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["Consigliere:ForwardedHeaders:KnownNetworks:0"] = "not-a-cidr",
            ["Consigliere:ForwardedHeaders:KnownNetworks:1"] = "10.0.0.0/8",
        });

        Assert.Single(options.KnownNetworks);
        Assert.Equal(8, options.KnownNetworks[0].PrefixLength);
    }

    [Theory]
    [InlineData("172.16.0.0/12", true, 12)]
    [InlineData("10.0.0.0/8", true, 8)]
    [InlineData("::1/128", true, 128)]
    [InlineData("10.0.0.0/40", false, 0)]   // prefix > 32 for IPv4
    [InlineData("garbage", false, 0)]
    [InlineData("10.0.0.0", false, 0)]      // no prefix
    public void TryParseNetwork_matrix(string cidr, bool ok, int expectedLen)
    {
        var parsed = ProxyHeadersSetup.TryParseNetwork(cidr, out var network);
        Assert.Equal(ok, parsed);
        if (ok) Assert.Equal(expectedLen, network.PrefixLength);
    }

    private static ForwardedHeadersOptions BuildOptions(Dictionary<string, string?> config)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config)
            .Build();
        var services = new ServiceCollection();
        services.AddOptions();
        services.AddConsigliereForwardedHeaders(configuration);
        return services.BuildServiceProvider()
            .GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;
    }
}
