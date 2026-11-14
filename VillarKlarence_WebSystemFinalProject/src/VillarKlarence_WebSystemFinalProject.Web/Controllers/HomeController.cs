using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Web.Controllers;

public class HomeController : Controller
{
    /// <summary>"/" → the login page (login.js forwards signed-in users to their dashboard).</summary>
    public IActionResult Index() => RedirectToAction("Login", "Account");

    /// <summary>Re-executed by UseStatusCodePagesWithReExecute for empty error responses.</summary>
    [Route("Home/Status")]
    public IActionResult Status(int code)
    {
        Response.StatusCode = code is >= 400 and <= 599 ? code : 404;
        if (code == 404) return View("NotFound");
        ViewData["StatusCode"] = Response.StatusCode;
        return View("Error");
    }

    [Route("Home/NotFound")]
    public new IActionResult NotFound()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("NotFound");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["StatusCode"] = 500;
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }
}
