using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

[Route("api/subjects")]
[Authorize(Roles = AppRoles.Faculty)]
public sealed class SubjectsController(ILookupService lookups) : ApiControllerBase
{
    /// <summary>GET /api/subjects — distinct SubjectCode/SubjectName pairs from StudentGrades (autocomplete).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SubjectOptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubjectOptionResponse>>> GetAll(CancellationToken ct) =>
        Ok(await lookups.GetSubjectsAsync(ct));
}
