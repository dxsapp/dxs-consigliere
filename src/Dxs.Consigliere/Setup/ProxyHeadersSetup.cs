using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

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
    /// KnownNetworks / KnownProxies are cleared so the bridge
    /// network's loopback peer (Caddy) is auto-trusted. Production
    /// deployments behind a known LB tighten this via
    /// `ForwardedHeadersOptions__KnownProxies__0=...` env.
    /// </summary>
    public static IServiceCollection AddConsigliereForwardedHeaders(this IServiceCollection services)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                       | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });
        return services;
    }
}
