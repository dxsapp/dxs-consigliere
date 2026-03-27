using Dxs.Consigliere.Configs;
using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminAuthControllerTests
{
    [Fact]
    public void Me_ReturnsDisabledStatus_WhenAdminAuthDisabled()
    {
        var controller = new AdminAuthController();
        var authService = CreateAuthService(new ConsigliereAdminAuthConfig { Enabled = false });

        var result = controller.Me(authService);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminAuthStatusResponse>(ok.Value);
        Assert.False(payload.Enabled);
        Assert.True(payload.Authenticated);
        Assert.Equal("disabled", payload.Mode);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForInvalidCredentials()
    {
        var controller = new AdminAuthController();
        var authService = CreateAuthService(new ConsigliereAdminAuthConfig
        {
            Enabled = true,
            Username = "operator",
            PasswordHash = ConsigliereAdminPasswordHash.Hash("correct-password")
        });

        var result = await controller.Login(
            new AdminLoginRequest { Username = "operator", Password = "wrong-password" },
            authService);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Login_SetsCookieSession_ForValidCredentials()
    {
        var controller = new AdminAuthController();
        var authService = CreateAuthService(new ConsigliereAdminAuthConfig
        {
            Enabled = true,
            Username = "operator",
            PasswordHash = ConsigliereAdminPasswordHash.Hash("correct-password"),
            CookieName = "consigliere_admin_test",
            SessionTtlMinutes = 60
        });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = CreateHttpContext()
        };

        var result = await controller.Login(
            new AdminLoginRequest { Username = "operator", Password = "correct-password" },
            authService);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminAuthStatusResponse>(ok.Value);
        Assert.True(payload.Enabled);
        Assert.True(payload.Authenticated);
        Assert.Equal("operator", payload.Username);
        Assert.Contains("consigliere_admin_test", controller.HttpContext.Response.Headers.SetCookie.ToString());
    }

    [Fact]
    public void AdminAndOpsControllers_RequireAdminPolicy()
    {
        var adminAuthorize = Assert.Single(typeof(AdminController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));
        var opsAuthorize = Assert.Single(typeof(OpsController).GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true));

        Assert.Equal(AdminAuthDefaults.Policy, ((AuthorizeAttribute)adminAuthorize).Policy);
        Assert.Equal(AdminAuthDefaults.Policy, ((AuthorizeAttribute)opsAuthorize).Policy);
    }

    private static IConsigliereAdminAuthService CreateAuthService(ConsigliereAdminAuthConfig config)
        => new ConsigliereAdminAuthService(Options.Create(config));

    private static DefaultHttpContext CreateHttpContext()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOptions();
        services.AddDataProtection();
        services.AddAuthentication(AdminAuthDefaults.Scheme)
            .AddCookie(
                AdminAuthDefaults.Scheme,
                options =>
                {
                    options.Cookie.Name = "consigliere_admin_test";
                    options.Events = new CookieAuthenticationEvents();
                });

        var provider = services.BuildServiceProvider();
        return new DefaultHttpContext { RequestServices = provider };
    }
}
