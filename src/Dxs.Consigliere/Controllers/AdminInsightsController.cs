using Dxs.Consigliere.Data.Tracking;
using Dxs.Consigliere.Dto.Responses.Admin;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin")]
[Authorize(Policy = AdminAuthDefaults.Policy)]
public class AdminInsightsController : BaseController
{
    [HttpGet("findings")]
    [Produces(typeof(AdminFindingResponse[]))]
    public async Task<IActionResult> GetFindings(
        [FromQuery] int take,
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
        => Ok(await queryService.GetFindingsAsync(take <= 0 ? 100 : take, cancellationToken));

    [HttpGet("dashboard/summary")]
    [Produces(typeof(AdminDashboardSummaryResponse))]
    public async Task<IActionResult> GetDashboardSummary(
        [FromServices] IAdminTrackingQueryService queryService,
        CancellationToken cancellationToken = default)
        => Ok(await queryService.GetDashboardSummaryAsync(cancellationToken));
}
