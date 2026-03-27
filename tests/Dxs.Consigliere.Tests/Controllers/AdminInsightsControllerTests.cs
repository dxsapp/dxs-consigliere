using Dxs.Consigliere.Controllers;
using Dxs.Consigliere.Data.Models.Tracking;
using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Responses.Admin;

using Microsoft.AspNetCore.Mvc;

using Moq;

namespace Dxs.Consigliere.Tests.Controllers;

public class AdminInsightsControllerTests
{
    [Fact]
    public async Task GetFindings_ReturnsQueryResults()
    {
        var queryService = new Mock<IAdminTrackingQueryService>(MockBehavior.Strict);
        queryService.Setup(x => x.GetFindingsAsync(25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new AdminFindingResponse
                {
                    EntityType = TrackedEntityType.Token,
                    EntityId = "token-1",
                    Code = "blocking_unknown_root",
                    Severity = "error",
                    Message = "rogue-root-1"
                }
            ]);

        var controller = new AdminInsightsController();

        var result = await controller.GetFindings(25, queryService.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminFindingResponse[]>(ok.Value);
        Assert.Single(payload);
        Assert.Equal("blocking_unknown_root", payload[0].Code);
    }

    [Fact]
    public async Task GetDashboardSummary_ReturnsQueryResults()
    {
        var queryService = new Mock<IAdminTrackingQueryService>(MockBehavior.Strict);
        queryService.Setup(x => x.GetDashboardSummaryAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AdminDashboardSummaryResponse
            {
                ActiveAddressCount = 2,
                ActiveTokenCount = 1,
                UnknownRootFindingCount = 3,
            });

        var controller = new AdminInsightsController();

        var result = await controller.GetDashboardSummary(queryService.Object);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<AdminDashboardSummaryResponse>(ok.Value);
        Assert.Equal(2, payload.ActiveAddressCount);
        Assert.Equal(3, payload.UnknownRootFindingCount);
    }
}
