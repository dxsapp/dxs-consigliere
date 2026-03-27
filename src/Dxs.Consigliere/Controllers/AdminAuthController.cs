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
    public IActionResult Me([FromServices] IConsigliereAdminAuthService authService)
        => Ok(BuildStatus(authService, User));

    [AllowAnonymous]
    [HttpPost("login")]
    [Produces(typeof(AdminAuthStatusResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(
        [FromBody] AdminLoginRequest request,
        [FromServices] IConsigliereAdminAuthService authService
    )
    {
        if (!authService.Enabled)
            return Ok(BuildDisabledStatus());

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { code = "credentials_required" });

        if (!authService.ValidateCredentials(request.Username, request.Password))
            return Unauthorized(new { code = "invalid_credentials" });

        var principal = authService.CreatePrincipal();
        await HttpContext.SignInAsync(
            AdminAuthDefaults.Scheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(authService.SessionTtlMinutes)
            });

        return Ok(BuildStatus(authService, principal));
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [Produces(typeof(AdminAuthStatusResponse))]
    public async Task<IActionResult> Logout([FromServices] IConsigliereAdminAuthService authService)
    {
        if (!authService.Enabled)
            return Ok(BuildDisabledStatus());

        await HttpContext.SignOutAsync(AdminAuthDefaults.Scheme);
        return Ok(BuildStatus(authService, new ClaimsPrincipal()));
    }

    private static AdminAuthStatusResponse BuildStatus(IConsigliereAdminAuthService authService, ClaimsPrincipal user)
    {
        if (!authService.Enabled)
            return BuildDisabledStatus();

        var authenticated = authService.IsAuthenticated(user);
        return new AdminAuthStatusResponse
        {
            Enabled = true,
            Authenticated = authenticated,
            Mode = "cookie",
            Username = authenticated ? authService.Username : string.Empty,
            SessionTtlMinutes = authService.SessionTtlMinutes
        };
    }

    private static AdminAuthStatusResponse BuildDisabledStatus()
        => new()
        {
            Enabled = false,
            Authenticated = true,
            Mode = "disabled"
        };
}
