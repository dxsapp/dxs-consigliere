namespace Dxs.Consigliere.Configs;

public sealed class ConsigliereAdminAuthConfig
{
    public int SessionTtlMinutes { get; init; } = 480;
    public string CookieName { get; init; } = "consigliere_admin";
}
