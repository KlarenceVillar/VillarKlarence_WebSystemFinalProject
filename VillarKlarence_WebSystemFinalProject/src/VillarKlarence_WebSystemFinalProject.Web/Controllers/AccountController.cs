using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Web.Controllers;

/// <summary>
/// Serves the sign-in and access-denied pages. Sign-in itself happens in wwwroot/js/login.js,
/// which calls POST /api/auth/login with fetch().
/// </summary>
public class AccountController : Controller
{
    public IActionResult Login() => View();

    public IActionResult AccessDenied()
    {
        Response.StatusCode = StatusCodes.Status403Forbidden;
        return View();
    }
}
