using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Web.Controllers;

/// <summary>
/// Serves the faculty dashboard shell. The page has no data of its own: faculty-dashboard.js loads
/// everything from the API, and the API enforces [Authorize(Roles = "Faculty")] on every call.
/// </summary>
public class FacultyController : Controller
{
    public IActionResult Index() => View();
}
