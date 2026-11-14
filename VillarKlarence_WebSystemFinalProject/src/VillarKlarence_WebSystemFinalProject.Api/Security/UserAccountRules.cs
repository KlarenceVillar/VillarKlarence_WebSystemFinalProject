using VillarKlarence_WebSystemFinalProject.Api.Models;

namespace VillarKlarence_WebSystemFinalProject.Api.Security;

/// <summary>
/// Enforces the account-to-profile rule:
///   Student account  => StudentId set, FacultyId null
///   Faculty account  => FacultyId set, StudentId null
/// Used by the seeder (when creating/repairing accounts) and by login (before a token is issued).
/// The database also enforces "not both" with CK_AspNetUsers_SingleAcademicProfile.
/// </summary>
public static class UserAccountRules
{
    /// <returns>null when valid, otherwise a description of the problem.</returns>
    public static string? Validate(ApplicationUser user, string role) => role switch
    {
        AppRoles.Student when user.StudentId is null => "Student accounts must be linked to a StudentId.",
        AppRoles.Student when user.FacultyId is not null => "Student accounts must not have a FacultyId.",
        AppRoles.Faculty when user.FacultyId is null => "Faculty accounts must be linked to a FacultyId.",
        AppRoles.Faculty when user.StudentId is not null => "Faculty accounts must not have a StudentId.",
        AppRoles.Student or AppRoles.Faculty => null,
        _ => $"Unknown role '{role}'."
    };
}
