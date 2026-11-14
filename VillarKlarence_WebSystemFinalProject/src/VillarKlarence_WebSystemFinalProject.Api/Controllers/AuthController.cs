using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

[Route("api/auth")]
public sealed class AuthController(IAccountService accounts) : ApiControllerBase
{
    /// <summary>POST /api/auth/login — checks the password with ASP.NET Core Identity and returns a JWT.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await accounts.LoginAsync(request.Email!, request.Password!, ct);

        return result.Status switch
        {
            LoginStatus.Success => Ok(result.Response),
            LoginStatus.AccountNotLinked => ForbiddenProblem(result.Message!),
            LoginStatus.LockedOut => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Account locked", detail: result.Message),
            _ => Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Login failed", detail: result.Message)
        };
    }

    /// <summary>GET /api/auth/me — profile of the signed-in user (name, role, student/faculty number).</summary>
    [HttpGet("me")]
    [Authorize(Roles = AppRoles.FacultyOrStudent)]
    [ProducesResponseType<UserInfoResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserInfoResponse>> Me(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var profile = userId is null ? null : await accounts.GetProfileAsync(userId, ct);

        return profile is null
            ? Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Unauthorized",
                detail: "Your account is no longer valid. Please sign in again.")
            : Ok(profile);
    }
}
