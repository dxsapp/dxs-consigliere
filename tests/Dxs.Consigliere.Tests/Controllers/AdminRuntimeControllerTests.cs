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

public class AdminRuntimeControllerTests
{
    [Fact]
    public async Task GetRuntimeSources_ReturnsServiceSnapshot()
    {
        var service = new Mock<IAdminRuntimeSourcePolicyService>(MockBehavior.Strict);
        service.Setup(x => x.GetRuntimeSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRuntimeSourcesResponse
            {
                RealtimePolicy = new AdminRealtimeSourcePolicyResponse
                {
                    OverrideActive = true
                }
            });

        var controller = new AdminRuntimeController();

        var result = await controller.GetRuntimeSources(service.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminRuntimeSourcesResponse>(ok.Value);
        Assert.True(payload.RealtimePolicy.OverrideActive);
    }

    [Fact]
    public async Task UpdateRealtimePolicy_ReturnsBadRequestWhenServiceRejectsRequest()
    {
        var service = new Mock<IAdminRuntimeSourcePolicyService>(MockBehavior.Strict);
        service.Setup(x => x.ApplyRealtimePolicyAsync("whatsonchain", "pipes", "admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRealtimeSourcePolicyMutationResult(false, "invalid_primary_realtime_source"));
        var auth = new Mock<IConsigliereAdminAuthService>(MockBehavior.Strict);
        auth.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsigliereAdminAuthState(false, true, true, "admin"));

        var controller = new AdminRuntimeController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        var result = await controller.UpdateRealtimePolicy(
            new AdminRealtimeSourcePolicyUpdateRequest
            {
                PrimaryRealtimeSource = "whatsonchain",
                BitailsTransport = "pipes"
            },
            service.Object,
            auth.Object);

        var badRequest = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
        Assert.Equal("invalid_primary_realtime_source", badRequest.Value?.GetType().GetProperty("code")?.GetValue(badRequest.Value));
    }

    [Fact]
    public async Task UpdateRealtimePolicy_UsesAuthenticatedUsernameAndReturnsSnapshot()
    {
        var service = new Mock<IAdminRuntimeSourcePolicyService>(MockBehavior.Strict);
        service.Setup(x => x.ApplyRealtimePolicyAsync("bitails", "zmq", "operator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRealtimeSourcePolicyMutationResult(true));
        service.Setup(x => x.GetRuntimeSourcesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRuntimeSourcesResponse
            {
                RealtimePolicy = new AdminRealtimeSourcePolicyResponse
                {
                    OverrideActive = true,
                    UpdatedBy = "operator"
                }
            });
        var auth = new Mock<IConsigliereAdminAuthService>(MockBehavior.Strict);
        auth.Setup(x => x.GetStateAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ConsigliereAdminAuthState(false, true, true, "admin"));

        var controller = new AdminRuntimeController
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

        var result = await controller.UpdateRealtimePolicy(
            new AdminRealtimeSourcePolicyUpdateRequest
            {
                PrimaryRealtimeSource = "bitails",
                BitailsTransport = "zmq"
            },
            service.Object,
            auth.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminRuntimeSourcesResponse>(ok.Value);
        Assert.Equal("operator", payload.RealtimePolicy.UpdatedBy);
    }

    [Fact]
    public async Task ResetRealtimePolicy_ReturnsServiceSnapshot()
    {
        var service = new Mock<IAdminRuntimeSourcePolicyService>(MockBehavior.Strict);
        service.Setup(x => x.ResetRealtimePolicyAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminRuntimeSourcesResponse
            {
                RealtimePolicy = new AdminRealtimeSourcePolicyResponse
                {
                    OverrideActive = false
                }
            });

        var controller = new AdminRuntimeController();

        var result = await controller.ResetRealtimePolicy(service.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminRuntimeSourcesResponse>(ok.Value);
        Assert.False(payload.RealtimePolicy.OverrideActive);
    }
}
