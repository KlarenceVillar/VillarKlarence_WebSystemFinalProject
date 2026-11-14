using Microsoft.AspNetCore.Identity;

namespace VillarKlarence_WebSystemFinalProject.Api.Models;

/// <summary>
/// ASP.NET Core Identity account. Passwords are stored only as Identity password hashes in dbo.AspNetUsers.
/// A Student account links to dbo.Students through <see cref="StudentId"/> (and has no FacultyId);
/// a Faculty account links to dbo.Faculty through <see cref="FacultyId"/> (and has no StudentId).
/// </summary>
public class ApplicationUser : IdentityUser
{
    public int? StudentId { get; set; }
    public Student? Student { get; set; }

    public int? FacultyId { get; set; }
    public Faculty? Faculty { get; set; }
}
