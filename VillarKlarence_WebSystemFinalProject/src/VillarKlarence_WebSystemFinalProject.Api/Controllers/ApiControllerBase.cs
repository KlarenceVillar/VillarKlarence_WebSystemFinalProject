using Microsoft.AspNetCore.Mvc;
using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Api.Controllers;

/// <summary>
/// Shared helpers that turn service outcomes into ProblemDetails responses.
/// (No [Produces] attribute on purpose: it would force errors to "application/json"
/// instead of the standard "application/problem+json".)
/// </summary>
[ApiController]
public abstract class ApiControllerBase : ControllerBase
{
    protected ObjectResult NotFoundProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: detail);

    protected ObjectResult ForbiddenProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: detail);

    protected ObjectResult ConflictProblem(string detail) =>
        Problem(statusCode: StatusCodes.Status409Conflict, title: "Duplicate grade record", detail: detail);

    /// <summary>Maps a failed <see cref="ServiceResult{T}"/> to 400 / 403 / 404 / 409.</summary>
    protected ActionResult FromFailure<T>(ServiceResult<T> result)
    {
        switch (result.Status)
        {
            case ServiceStatus.ValidationFailed:
                foreach (var (field, messages) in result.Errors)
                    foreach (var message in messages)
                        ModelState.AddModelError(field, message);
                return ValidationProblem(ModelState);
            case ServiceStatus.NotFound:
                return NotFoundProblem(result.Message ?? "The requested record was not found.");
            case ServiceStatus.Conflict:
                return ConflictProblem(result.Message ?? "A matching record already exists.");
            case ServiceStatus.Forbidden:
                return ForbiddenProblem(result.Message ?? "You do not have permission to perform this action.");
            default:
                throw new InvalidOperationException($"Unexpected service status {result.Status}.");
        }
    }
}
