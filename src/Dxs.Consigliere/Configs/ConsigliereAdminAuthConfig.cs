namespace Dxs.Consigliere.Configs;

public sealed class ConsigliereAdminAuthConfig
{
    public bool Enabled { get; init; }
    public string Username { get; init; } = string.Empty;
    public string PasswordHash { get; init; } = string.Empty;
    public int SessionTtlMinutes { get; init; } = 480;
    public string CookieName { get; init; } = "consigliere_admin";
}
