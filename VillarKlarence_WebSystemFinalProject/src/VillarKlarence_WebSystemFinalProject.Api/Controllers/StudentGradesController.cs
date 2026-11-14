using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

[Route("api/student/grades")]
[Authorize(Roles = AppRoles.Student)]
public sealed class StudentGradesController(IGradeService grades) : ApiControllerBase
{
    /// <summary>
    /// GET /api/student/grades — the signed-in student's own grades.
    /// OWNERSHIP: the StudentId comes ONLY from the "student_id" claim of the validated JWT.
    /// This action has no studentId parameter, so ?studentId=2 in the URL is simply ignored.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<GradeResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<GradeResponse>>> GetMine(CancellationToken ct)
    {
        var studentId = User.GetStudentId();
        if (studentId is null)
            return ForbiddenProblem("Your account is not linked to a student record.");

        return Ok(await grades.GetForStudentAsync(studentId.Value, ct));
    }
}
