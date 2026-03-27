using System.Security.Claims;

using Dxs.Consigliere.Configs;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public interface IConsigliereAdminAuthService
{
    bool Enabled { get; }
    string Username { get; }
    int SessionTtlMinutes { get; }
    bool ValidateCredentials(string username, string password);
    bool IsAuthenticated(ClaimsPrincipal principal);
    ClaimsPrincipal CreatePrincipal();
}

public sealed class ConsigliereAdminAuthService(IOptions<ConsigliereAdminAuthConfig> config)
    : IConsigliereAdminAuthService
{
    private readonly ConsigliereAdminAuthConfig _config = config.Value;

    public bool Enabled => _config.Enabled;
    public string Username => _config.Username;
    public int SessionTtlMinutes => _config.SessionTtlMinutes;

    public bool ValidateCredentials(string username, string password)
    {
        if (!Enabled)
            return true;

        return string.Equals(username, _config.Username, StringComparison.Ordinal)
               && ConsigliereAdminPasswordHash.Verify(password, _config.PasswordHash);
    }

    public bool IsAuthenticated(ClaimsPrincipal principal)
        => principal?.Identity?.IsAuthenticated == true
           && principal.HasClaim(AdminAuthDefaults.AdminClaimType, "true")
           && string.Equals(principal.Identity?.Name, _config.Username, StringComparison.Ordinal);

    public ClaimsPrincipal CreatePrincipal()
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, _config.Username),
            new Claim(AdminAuthDefaults.AdminClaimType, "true")
        };

        var identity = new ClaimsIdentity(claims, AdminAuthDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }
}
