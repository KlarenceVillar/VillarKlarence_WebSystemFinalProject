using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Web.Controllers;

/// <summary>
/// Serves the student dashboard shell. student-dashboard.js loads the signed-in student's own grades
/// from GET /api/student/grades (the API derives the student from the JWT).
/// </summary>
public class StudentController : Controller
{
    public IActionResult Index() => View();
}
