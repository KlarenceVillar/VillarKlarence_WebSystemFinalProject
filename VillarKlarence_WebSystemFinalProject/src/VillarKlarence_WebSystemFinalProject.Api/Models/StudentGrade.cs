namespace VillarKlarence_WebSystemFinalProject.Api.Models;

/// <summary>Maps to dbo.StudentGrades, created by Database/StudentGradeManagementDB.sql (not by EF migrations).</summary>
public class StudentGrade
{
    public int GradeId { get; set; }
    public int StudentId { get; set; }
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;

    /// <summary>The faculty member who originally posted the grade. Always taken from the JWT, never from the request body.</summary>
    public int FacultyId { get; set; }

    public int Grade { get; set; }

    /// <summary>Always computed by <see cref="Services.IRemarksCalculator"/> on the server.</summary>
    public string Remarks { get; set; } = string.Empty;

    public string Semester { get; set; } = string.Empty;
    public string SchoolYear { get; set; } = string.Empty;
    public DateTime DatePosted { get; set; }
    public DateTime? DateModified { get; set; }

    public Student? Student { get; set; }
    public Faculty? Faculty { get; set; }
}
