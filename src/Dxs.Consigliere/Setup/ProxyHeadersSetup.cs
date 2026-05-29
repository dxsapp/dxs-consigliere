using System.Net;
using Dxs.Consigliere.Configs;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

// .NET 8+ added System.Net.IPNetwork, which collides with the
// framework's Microsoft.AspNetCore.HttpOverrides.IPNetwork that
// ForwardedHeadersOptions.KnownNetworks actually uses.
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;

namespace Dxs.Consigliere.Setup;

public static class ProxyHeadersSetup
{
    /// <summary>
    /// wave-A3 S0 — Kestrel runs behind Caddy. This wires the
    /// X-Forwarded-{Proto,For} translation so that downstream
    /// middleware sees the client's true scheme + IP address. The
    /// cookie `SecurePolicy=Always` needs Request.Scheme to read
    /// `https`, and the S1 rate limiter keys by client IP — both
    /// break silently without this.
    ///
    /// <para>S0/S1-audit L1+L2: the trusted-proxy list is now
    /// operator-tunable via <see cref="ForwardedHeadersTrustConfig"/>
    /// (string CIDRs / IPs that DO bind from env, unlike the
    /// framework's <c>IPAddress</c>-typed lists). Set
    /// <c>Consigliere__ForwardedHeaders__KnownNetworks__0=172.16.0.0/12</c>
    /// (or a specific <c>KnownProxies</c> IP) to restrict which hop
    /// the X-Forwarded-* headers are trusted from. When no list is
    /// configured the middleware keeps its permissive default —
    /// safe ONLY behind a header-overwriting proxy (Caddy) with
    /// Kestrel unpublished; see the config type's remarks.</para>
    /// </summary>
    public static IServiceCollection AddConsigliereForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var trust = configuration.GetSection("Consigliere:ForwardedHeaders").Get<ForwardedHeadersTrustConfig>()
                    ?? new ForwardedHeadersTrustConfig();

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                       | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();

            if (!trust.HasTrustList)
            {
                // No explicit trust list — permissive default
                // (consume X-Forwarded-* from any immediate caller).
                // Safe behind Caddy (which overwrites the header) +
                // unpublished Kestrel. Documented fallback.
                return;
            }

            foreach (var cidr in trust.KnownNetworks)
            {
                if (TryParseNetwork(cidr, out var network))
                    options.KnownNetworks.Add(network);
            }
            foreach (var ip in trust.KnownProxies)
            {
                if (IPAddress.TryParse(ip?.Trim(), out var addr))
                    options.KnownProxies.Add(addr);
            }
        });
        return services;
    }

    /// <summary>
    /// Parses a CIDR string (<c>a.b.c.d/n</c>) into the framework's
    /// <see cref="IPNetwork"/>. Returns false on malformed input so
    /// one bad config entry doesn't take down host startup.
    /// </summary>
    internal static bool TryParseNetwork(string cidr, out IPNetwork network)
    {
        network = default;
        if (string.IsNullOrWhiteSpace(cidr)) return false;
        var parts = cidr.Trim().Split('/', 2);
        if (parts.Length != 2) return false;
        if (!IPAddress.TryParse(parts[0], out var prefix)) return false;
        if (!int.TryParse(parts[1], out var prefixLength) || prefixLength < 0) return false;
        var maxLen = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 ? 128 : 32;
        if (prefixLength > maxLen) return false;
        network = new IPNetwork(prefix, prefixLength);
        return true;
    }
}
