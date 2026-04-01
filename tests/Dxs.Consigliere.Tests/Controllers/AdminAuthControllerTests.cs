using System.Security.Claims;

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

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminAuthControllerTests
{
    [Fact]
    public async Task Me_ReturnsSetupRequiredStatus_WhenBootstrapIncomplete()
    {
        var controller = new AdminAuthController();
        var authService = CreateAuthService(new ConsigliereAdminAuthState(true, false, false, string.Empty));

        var result = await controller.Me(authService.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminAuthStatusResponse>(ok.Value);
        Assert.True(payload.SetupRequired);
        Assert.False(payload.Authenticated);
    }

    [Fact]
    public async Task Login_ReturnsUnauthorized_ForInvalidCredentials()
    {
        var controller = new AdminAuthController();
        var authService = CreateAuthService(new ConsigliereAdminAuthState(false, true, false, string.Empty), validateCredentials: false);

        var result = await controller.Login(
            new AdminLoginRequest { Username = "operator", Password = "wrong-password" },
            authService.Object);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Equal(401, unauthorized.StatusCode);
    }

    [Fact]
    public async Task Login_SetsCookieSession_ForValidCredentials()
    {
        var authService = CreateAuthService(new ConsigliereAdminAuthState(false, true, false, string.Empty), validateCredentials: true);
        authService.Setup(x => x.CreatePrincipal("operator"))
            .Returns(new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, "operator"),
                new Claim(AdminAuthDefaults.AdminClaimType, "true")
            ], AdminAuthDefaults.Scheme)));
        authService.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsigliereAdminAuthState(false, true, true, "operator"));

        var controller = new AdminAuthController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = CreateHttpContext()
            }
        };

        var result = await controller.Login(
            new AdminLoginRequest { Username = "operator", Password = "correct-password" },
            authService.Object);

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

    private static Mock<IConsigliereAdminAuthService> CreateAuthService(
        ConsigliereAdminAuthState state,
        bool validateCredentials = false)
    {
        var auth = new Mock<IConsigliereAdminAuthService>(MockBehavior.Strict);
        auth.SetupGet(x => x.SessionTtlMinutes).Returns(60);
        auth.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(state);
        auth.Setup(x => x.ValidateCredentialsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(validateCredentials);
        return auth;
    }

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
