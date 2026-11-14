using System.ComponentModel.DataAnnotations;
using VillarKlarence_WebSystemFinalProject.Api.Validation;

namespace VillarKlarence_WebSystemFinalProject.Api.Dtos;

/// <summary>
/// Fields a faculty member may send when creating or updating a grade.
/// SECURITY: there is intentionally NO Remarks property and NO FacultyId property.
/// Any "remarks" or "facultyId" sent by a client is simply ignored by the JSON deserializer.
/// Remarks are computed by the server; FacultyId comes from the JWT.
/// </summary>
public abstract class GradeWriteRequest
{
    [Required(ErrorMessage = "Student is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Student is required.")]
    public int? StudentId { get; set; }

    [Required(ErrorMessage = "Subject code is required.")]
    [StringLength(30, MinimumLength = 2, ErrorMessage = "Subject code must be 2 to 30 characters.")]
    [RegularExpression(@"^[A-Za-z0-9][A-Za-z0-9\- ]*$",
        ErrorMessage = "Subject code may contain only letters, numbers, spaces and hyphens.")]
    public string? SubjectCode { get; set; }

    [Required(ErrorMessage = "Subject name is required.")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Subject name must be 3 to 200 characters.")]
    public string? SubjectName { get; set; }

    /// <summary>
    /// int? + [Required] distinguishes "missing" from 0. Decimals (87.5) and strings ("abc")
    /// fail JSON conversion and are turned into a 400 by Program.cs (InvalidModelStateResponseFactory).
    /// </summary>
    [Required(ErrorMessage = GradeRules.GradeRequiredMessage)]
    [Range(GradeRules.MinGrade, GradeRules.MaxGrade, ErrorMessage = GradeRules.GradeMessage)]
    public int? Grade { get; set; }

    [Required(ErrorMessage = "Semester is required.")]
    [StringLength(50)]
    [Semester]
    public string? Semester { get; set; }

    [Required(ErrorMessage = "School year is required.")]
    [StringLength(20)]
    [SchoolYear]
    public string? SchoolYear { get; set; }
}

/// <summary>POST /api/grades body.</summary>
public sealed class GradeCreateRequest : GradeWriteRequest;

/// <summary>PUT /api/grades/{id} body.</summary>
public sealed class GradeUpdateRequest : GradeWriteRequest;

/// <summary>Grade record returned by every grade endpoint. Remarks is always the server-computed value.</summary>
public sealed record GradeResponse
{
    public int GradeId { get; init; }
    public int StudentId { get; init; }
    public string StudentNumber { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string SubjectCode { get; init; } = string.Empty;
    public string SubjectName { get; init; } = string.Empty;
    public int FacultyId { get; init; }
    public string FacultyName { get; init; } = string.Empty;
    public int Grade { get; init; }
    public string Remarks { get; init; } = string.Empty;
    public string Semester { get; init; } = string.Empty;
    public string SchoolYear { get; init; } = string.Empty;
    public DateTime DatePosted { get; init; }
    public DateTime? DateModified { get; init; }
}

/// <summary>Optional filters for GET /api/grades.</summary>
public sealed class GradeQuery
{
    public int? StudentId { get; set; }
    public string? SubjectCode { get; set; }
    public string? Semester { get; set; }
    public string? SchoolYear { get; set; }
    public string? Search { get; set; }
}
