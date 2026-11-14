using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

[Route("api/students")]
[Authorize(Roles = AppRoles.Faculty)]
public sealed class StudentsController(ILookupService lookups) : ApiControllerBase
{
    /// <summary>GET /api/students — active students for the grade form dropdown.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<StudentOptionResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentOptionResponse>>> GetAll(CancellationToken ct) =>
        Ok(await lookups.GetActiveStudentsAsync(ct));
}
