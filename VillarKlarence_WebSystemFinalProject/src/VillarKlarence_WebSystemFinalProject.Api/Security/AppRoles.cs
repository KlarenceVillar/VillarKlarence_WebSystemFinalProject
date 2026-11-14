namespace VillarKlarence_WebSystemFinalProject.Api.Security;

public static class AppRoles
{
    public const string Faculty = "Faculty";
    public const string Student = "Student";

    /// <summary>For [Authorize(Roles = ...)] on endpoints that both roles may call.</summary>
    public const string FacultyOrStudent = Faculty + "," + Student;

    public static readonly string[] All = [Faculty, Student];
}
