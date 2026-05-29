namespace Dxs.Consigliere.Configs;

/// <summary>
/// wave-A3 S0/S1-audit L1+L2 — operator-tunable trust list for the
/// X-Forwarded-* middleware. The framework's
/// <c>ForwardedHeadersOptions.KnownProxies</c> /
/// <c>KnownNetworks</c> are <c>IList&lt;IPAddress&gt;</c> /
/// <c>IList&lt;IPNetwork&gt;</c> — neither round-trips through the
/// .NET configuration binder, so there was no working env knob to
/// tighten the (default trust-all) behaviour. These STRING lists
/// bind cleanly from env:
///
///   Consigliere__ForwardedHeaders__KnownNetworks__0=172.16.0.0/12
///   Consigliere__ForwardedHeaders__KnownProxies__0=10.0.0.5
///
/// <para>When BOTH are empty the middleware keeps its permissive
/// default (consume X-Forwarded-* from any immediate caller). That
/// is safe ONLY behind a proxy that overwrites the header (the
/// bundled Caddy does, via `header_up X-Forwarded-For`) AND with
/// Kestrel unpublished (compose `expose`, not `ports`). Any
/// deployment that drops Caddy or publishes :5000 MUST populate at
/// least one list, or an attacker can spoof X-Forwarded-For to
/// evade the per-IP rate limiter and the cookie `Secure`
/// logic.</para>
/// </summary>
public sealed class ForwardedHeadersTrustConfig
{
    /// <summary>CIDR strings (e.g. <c>172.16.0.0/12</c>) of trusted proxy networks.</summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>IP strings of individual trusted proxies.</summary>
    public string[] KnownProxies { get; init; } = [];

    public bool HasTrustList => KnownNetworks.Length > 0 || KnownProxies.Length > 0;
}
