using System.Globalization;
using System.Security.Claims;

namespace VillarKlarence_WebSystemFinalProject.Api.Security;

/// <summary>Reads the identity of the caller from the validated JWT claims (never from the request).</summary>
public static class ClaimsPrincipalExtensions
{
    public static string? GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>The caller's dbo.Students.StudentId from the "student_id" claim, or null.</summary>
    public static int? GetStudentId(this ClaimsPrincipal user) =>
        ParseInt(user.FindFirstValue(AppClaimTypes.StudentId));

    /// <summary>The caller's dbo.Faculty.FacultyId from the "faculty_id" claim, or null.</summary>
    public static int? GetFacultyId(this ClaimsPrincipal user) =>
        ParseInt(user.FindFirstValue(AppClaimTypes.FacultyId));

    private static int? ParseInt(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : null;
}
