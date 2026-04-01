using System.Security.Claims;

using Dxs.Consigliere.Dto.Requests;
using Dxs.Consigliere.Dto.Responses;
using Dxs.Consigliere.Setup;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dxs.Consigliere.Controllers;

[Route("api/admin/auth")]
public class AdminAuthController : BaseController
{
    [AllowAnonymous]
    [HttpGet("me")]
    [Produces(typeof(AdminAuthStatusResponse))]
    public async Task<IActionResult> Me(
        [FromServices] IConsigliereAdminAuthService authService,
        CancellationToken cancellationToken = default)
        => Ok(await BuildStatusAsync(authService, User, cancellationToken));

    [AllowAnonymous]
    [HttpPost("login")]
    [Produces(typeof(AdminAuthStatusResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequest request,
        [FromServices] IConsigliereAdminAuthService authService,
        CancellationToken cancellationToken = default
    )
    {
        var state = await authService.GetStateAsync(User, cancellationToken);
        if (state.SetupRequired)
            return Conflict(new { code = "setup_required" });
        if (!state.Enabled)
            return Ok(BuildDisabledStatus());

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { code = "credentials_required" });

        if (!await authService.ValidateCredentialsAsync(request.Username, request.Password, cancellationToken))
            return Unauthorized(new { code = "invalid_credentials" });

        var principal = authService.CreatePrincipal(request.Username.Trim());
        await HttpContext.SignInAsync(
            AdminAuthDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(authService.SessionTtlMinutes)
            });

        return Ok(await BuildStatusAsync(authService, principal, cancellationToken));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [Produces(typeof(AdminAuthStatusResponse))]
    public async Task<IActionResult> Logout(
        [FromServices] IConsigliereAdminAuthService authService,
        CancellationToken cancellationToken = default)
    {
        var state = await authService.GetStateAsync(User, cancellationToken);
        if (state.SetupRequired)
            return Ok(BuildSetupRequiredStatus());
        if (!state.Enabled)
            return Ok(BuildDisabledStatus());

        await HttpContext.SignOutAsync(AdminAuthDefaults.Scheme);
        return Ok(await BuildStatusAsync(authService, new ClaimsPrincipal(), cancellationToken));
    }

    private static async Task<AdminAuthStatusResponse> BuildStatusAsync(
        IConsigliereAdminAuthService authService,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var state = await authService.GetStateAsync(user, cancellationToken);
        if (state.SetupRequired)
            return BuildSetupRequiredStatus();

        if (!state.Enabled)
            return BuildDisabledStatus();

        return new AdminAuthStatusResponse
        {
            SetupRequired = false,
            Enabled = true,
            Authenticated = state.Authenticated,
            Mode = "cookie",
            Username = state.Authenticated ? state.Username : string.Empty,
            SessionTtlMinutes = authService.SessionTtlMinutes
        };
    }

    private static AdminAuthStatusResponse BuildDisabledStatus()
        => new()
        {
            SetupRequired = false,
            Enabled = false,
            Authenticated = true,
            Mode = "disabled"
        };

    private static AdminAuthStatusResponse BuildSetupRequiredStatus()
        => new()
        {
            SetupRequired = true,
            Enabled = false,
            Authenticated = false,
            Mode = "cookie"
        };
}
