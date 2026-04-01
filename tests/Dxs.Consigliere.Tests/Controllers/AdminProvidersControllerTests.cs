using System.Security.Claims;

using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Runtime;
using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminProvidersControllerTests
{
    [Fact]
    public async Task GetProviders_ReturnsServiceSnapshot()
    {
        var service = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        service.Setup(x => x.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProvidersResponse
            {
                Recommendations = new AdminProviderRecommendationsResponse
                {
                    RealtimePrimaryProvider = "bitails"
                }
            });

        var controller = new AdminProvidersController();
        var result = await controller.GetProviders(service.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProvidersResponse>(ok.Value);
        Assert.Equal("bitails", payload.Recommendations.RealtimePrimaryProvider);
    }

    [Fact]
    public async Task UpdateProvidersConfig_ReturnsBadRequestWhenServiceRejectsRequest()
    {
        var service = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        service.Setup(x => x.ApplyProviderConfigAsync(
                It.IsAny<AdminProviderConfigUpdateRequest>(),
                "admin",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderConfigMutationResult(false, "invalid_realtime_primary_provider"));

        var auth = new Mock<IConsigliereAdminAuthService>(MockBehavior.Strict);
        auth.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsigliereAdminAuthState(false, true, true, "admin"));

        var controller = new AdminProvidersController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateProvidersConfig(
            new AdminProviderConfigUpdateRequest(),
            service.Object,
            auth.Object);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("invalid_realtime_primary_provider", badRequest.Value?.GetType().GetProperty("code")?.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task UpdateProvidersConfig_UsesAuthenticatedUsernameAndReturnsSnapshot()
    {
        var service = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        service.Setup(x => x.ApplyProviderConfigAsync(
                It.IsAny<AdminProviderConfigUpdateRequest>(),
                "operator",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProviderConfigMutationResult(true));
        service.Setup(x => x.GetProvidersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProvidersResponse
            {
                Config = new AdminProviderConfigResponse
                {
                    OverrideActive = true,
                    UpdatedBy = "operator"
                }
            });

        var auth = new Mock<IConsigliereAdminAuthService>(MockBehavior.Strict);
        auth.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsigliereAdminAuthState(false, true, true, "admin"));

        var controller = new AdminProvidersController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, "operator")
                    ], "cookie"))
                }
            }
        };

        var result = await controller.UpdateProvidersConfig(
            new AdminProviderConfigUpdateRequest(),
            service.Object,
            auth.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProvidersResponse>(ok.Value);
        Assert.Equal("operator", payload.Config.UpdatedBy);
    }

    [Fact]
    public async Task ResetProvidersConfig_ReturnsServiceSnapshot()
    {
        var service = new Mock<IAdminProviderConfigService>(MockBehavior.Strict);
        service.Setup(x => x.ResetProviderConfigAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminProvidersResponse
            {
                Config = new AdminProviderConfigResponse
                {
                    OverrideActive = false
                }
            });

        var controller = new AdminProvidersController();
        var result = await controller.ResetProvidersConfig(service.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminProvidersResponse>(ok.Value);
        Assert.False(payload.Config.OverrideActive);
    }
}
