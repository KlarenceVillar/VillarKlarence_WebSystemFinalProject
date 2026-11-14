namespace VillarKlarence_WebSystemFinalProject.Api.Models;

/// <summary>Maps to dbo.Faculty, created by Database/StudentGradeManagementDB.sql (not by EF migrations).</summary>
public class Faculty
{
    public int FacultyId { get; set; }
    public string FacultyNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<StudentGrade> PostedGrades { get; set; } = new List<StudentGrade>();
}
