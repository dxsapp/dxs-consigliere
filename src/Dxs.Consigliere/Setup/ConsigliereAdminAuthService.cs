using System.Security.Claims;

using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Data.Models.Runtime;
using Dxs.Consigliere.Data.Runtime;

using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public interface IConsigliereAdminAuthService
{
    int SessionTtlMinutes { get; }
    Task<ConsigliereAdminAuthState> GetStateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
    ClaimsPrincipal CreatePrincipal(string username);
}

public sealed record ConsigliereAdminAuthState(
    bool SetupRequired,
    bool Enabled,
    bool Authenticated,
    string Username
);

public sealed class ConsigliereAdminAuthService(
    IOptions<ConsigliereAdminAuthConfig> config,
    ISetupBootstrapStore setupStore
) : IConsigliereAdminAuthService
{
    private readonly ConsigliereAdminAuthConfig _config = config.Value;

    public int SessionTtlMinutes => _config.SessionTtlMinutes;

    public Task<ConsigliereAdminAuthState> GetStateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var state = GetOrSeedState();
        if (!state.SetupCompleted)
            return Task.FromResult(new ConsigliereAdminAuthState(true, false, false, string.Empty));

        if (!state.AdminEnabled)
            return Task.FromResult(new ConsigliereAdminAuthState(false, false, true, string.Empty));

        var authenticated = principal?.Identity?.IsAuthenticated == true
                            && principal.HasClaim(AdminAuthDefaults.AdminClaimType, "true")
                            && string.Equals(principal.Identity?.Name, state.AdminUsername, StringComparison.Ordinal);

        return Task.FromResult(new ConsigliereAdminAuthState(false, true, authenticated, authenticated ? state.AdminUsername ?? string.Empty : string.Empty));
    }

    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        var state = GetOrSeedState();
        if (!state.SetupCompleted || !state.AdminEnabled)
            return Task.FromResult(false);

        return Task.FromResult(
            string.Equals(username, state.AdminUsername, StringComparison.Ordinal)
            && ConsigliereAdminPasswordHash.Verify(password, state.AdminPasswordHash));
    }

    public ClaimsPrincipal CreatePrincipal(string username)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, username),
            new Claim(AdminAuthDefaults.AdminClaimType, "true")
        };

        var identity = new ClaimsIdentity(claims, AdminAuthDefaults.Scheme, ClaimTypes.Name, ClaimTypes.Role);
        return new ClaimsPrincipal(identity);
    }

    private SetupBootstrapDocument GetOrSeedState()
    {
        var current = setupStore.Get();
        if (current is not null)
            return current;

        var seeded = new SetupBootstrapDocument
        {
            Id = SetupBootstrapDocument.DocumentId,
            SetupCompleted = false,
            AdminEnabled = false,
            AdminUsername = string.Empty,
            AdminPasswordHash = string.Empty,
            UpdatedBy = "system-defaults"
        };

        setupStore.SaveAsync(seeded).GetAwaiter().GetResult();
        return seeded;
    }
}
