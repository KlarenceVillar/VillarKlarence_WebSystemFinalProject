using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

/// <summary>
/// Faculty grade management. Roles are checked per action on the SERVER; hiding buttons in the
/// MVC pages is only a convenience. Students calling POST/PUT/DELETE get 403, no token gets 401.
/// </summary>
[Route("api/grades")]
public sealed class GradesController(IGradeService grades) : ApiControllerBase
{
    /// <summary>GET /api/grades?studentId=&amp;subjectCode=&amp;semester=&amp;schoolYear=&amp;search=</summary>
    [HttpGet]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType<IReadOnlyList<GradeResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<GradeResponse>>> GetAll([FromQuery] GradeQuery query, CancellationToken ct) =>
        Ok(await grades.GetAllAsync(query, ct));

    /// <summary>
    /// GET /api/grades/{id}. Faculty: any record. Student: only their own record;
    /// someone else's record answers 404 so its existence is not revealed.
    /// </summary>
    [HttpGet("{id:int}", Name = "GetGradeById")]
    [Authorize(Roles = AppRoles.FacultyOrStudent)]
    [ProducesResponseType<GradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<GradeResponse>> GetById(int id, CancellationToken ct)
    {
        int? ownerStudentId = null;
        if (!User.IsInRole(AppRoles.Faculty))
        {
            // Student caller: the owner filter comes from the JWT "student_id" claim only.
            ownerStudentId = User.GetStudentId();
            if (ownerStudentId is null)
                return ForbiddenProblem("Your account is not linked to a student record.");
        }

        var grade = await grades.GetByIdAsync(id, ownerStudentId, ct);
        return grade is null ? NotFoundProblem($"Grade record {id} was not found.") : Ok(grade);
    }

    /// <summary>POST /api/grades — 201 Created with Location header. Remarks computed by the server.</summary>
    [HttpPost]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType<GradeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GradeResponse>> Create([FromBody] GradeCreateRequest request, CancellationToken ct)
    {
        // FacultyId comes from the signed-in faculty's token, never from the request body.
        var facultyId = User.GetFacultyId();
        if (facultyId is null)
            return ForbiddenProblem("Your account is not linked to a faculty record.");

        var result = await grades.CreateAsync(request, facultyId.Value, ct);
        if (!result.Succeeded) return FromFailure(result);

        return CreatedAtRoute("GetGradeById", new { id = result.Value!.GradeId }, result.Value);
    }

    /// <summary>PUT /api/grades/{id} — 200 OK with the updated record. Remarks recomputed.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType<GradeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<GradeResponse>> Update(int id, [FromBody] GradeUpdateRequest request, CancellationToken ct)
    {
        var result = await grades.UpdateAsync(id, request, ct);
        return result.Succeeded ? Ok(result.Value) : FromFailure(result);
    }

    /// <summary>DELETE /api/grades/{id} — 204 No Content.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Faculty)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct) =>
        await grades.DeleteAsync(id, ct) ? NoContent() : NotFoundProblem($"Grade record {id} was not found.");
}
