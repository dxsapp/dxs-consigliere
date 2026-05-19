namespace Dxs.Consigliere.Configs;

public sealed class ConsigliereAdminAuthConfig
{
    public int SessionTtlMinutes { get; init; } = 480;
    public string CookieName { get; init; } = "consigliere_admin";

    // wave-A3 S0: cookie Secure flag is config-driven so the dev
    // profile (plain HTTP via the compose dev caddy with `tls
    // internal`, plus Playwright's local-loop runs without Caddy)
    // can opt down to `SameAsRequest` while production stays
    // `Always`. Bound to Microsoft.AspNetCore.Http.CookieSecurePolicy.
    public string CookieSecure { get; init; } = "Always";
}
