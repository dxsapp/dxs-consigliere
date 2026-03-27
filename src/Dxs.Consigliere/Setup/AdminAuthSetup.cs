using Dxs.Consigliere.Configs;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Setup;

public static class AdminAuthSetup
{
    public static IServiceCollection AddConsigliereAdminAuth(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var adminAuthConfig = configuration.GetSection("Consigliere:AdminAuth").Get<ConsigliereAdminAuthConfig>()
                              ?? new ConsigliereAdminAuthConfig();

        services
            .AddOptions<ConsigliereAdminAuthConfig>()
            .Bind(configuration.GetSection("Consigliere:AdminAuth"))
            .ValidateOnStart();

        services
            .AddSingleton<IValidateOptions<ConsigliereAdminAuthConfig>, ConsigliereAdminAuthConfigValidation>()
            .AddSingleton<IConsigliereAdminAuthService, ConsigliereAdminAuthService>()
            .AddSingleton<IAuthorizationHandler, ConsigliereAdminAccessHandler>();

        services
            .AddAuthentication(AdminAuthDefaults.Scheme)
            .AddCookie(
                AdminAuthDefaults.Scheme,
                options =>
                {
                    options.Cookie.Name = string.IsNullOrWhiteSpace(adminAuthConfig.CookieName)
                        ? "consigliere_admin"
                        : adminAuthConfig.CookieName;
                    options.Cookie.HttpOnly = true;
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.SlidingExpiration = true;
                    options.ExpireTimeSpan = TimeSpan.FromMinutes(adminAuthConfig.SessionTtlMinutes > 0
                        ? adminAuthConfig.SessionTtlMinutes
                        : 480);
                    options.Events = new CookieAuthenticationEvents
                    {
                        OnRedirectToLogin = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            return Task.CompletedTask;
                        },
                        OnRedirectToAccessDenied = context =>
                        {
                            context.Response.StatusCode = StatusCodes.Status403Forbidden;
                            return Task.CompletedTask;
                        }
                    };
                });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AdminAuthDefaults.Policy,
                policy => policy.Requirements.Add(new ConsigliereAdminAccessRequirement()));
        });

        return services;
    }
}

public sealed class ConsigliereAdminAccessRequirement : IAuthorizationRequirement;

public sealed class ConsigliereAdminAccessHandler(IConsigliereAdminAuthService authService)
    : AuthorizationHandler<ConsigliereAdminAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ConsigliereAdminAccessRequirement requirement)
    {
        if (!authService.Enabled || authService.IsAuthenticated(context.User))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
