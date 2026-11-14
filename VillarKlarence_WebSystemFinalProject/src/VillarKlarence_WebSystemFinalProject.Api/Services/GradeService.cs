using System.Linq.Expressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Models;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

/// <summary>
/// Grade business rules. Every query is EF Core LINQ (parameterized SQL); no SQL strings are built.
/// </summary>
public sealed class GradeService(
    AppDbContext db,
    IRemarksCalculator remarksCalculator,
    TimeProvider timeProvider,
    ILogger<GradeService> logger) : IGradeService
{
    private const int MaxSearchLength = 100;

    /// <summary>Projection used by every read so responses always have the same shape.</summary>
    private static readonly Expression<Func<StudentGrade, GradeResponse>> ToResponse = g => new GradeResponse
    {
        GradeId = g.GradeId,
        StudentId = g.StudentId,
        StudentNumber = g.Student!.StudentNumber,
        StudentName = g.Student.FirstName + " " + g.Student.LastName,
        SubjectCode = g.SubjectCode,
        SubjectName = g.SubjectName,
        FacultyId = g.FacultyId,
        FacultyName = g.Faculty!.FirstName + " " + g.Faculty.LastName,
        Grade = g.Grade,
        Remarks = g.Remarks,
        Semester = g.Semester,
        SchoolYear = g.SchoolYear,
        DatePosted = g.DatePosted,
        DateModified = g.DateModified
    };

    public async Task<IReadOnlyList<GradeResponse>> GetAllAsync(GradeQuery query, CancellationToken ct = default)
    {
        IQueryable<StudentGrade> grades = db.StudentGrades.AsNoTracking();

        if (query.StudentId is > 0)
            grades = grades.Where(g => g.StudentId == query.StudentId);

        if (!string.IsNullOrWhiteSpace(query.SubjectCode))
        {
            var code = query.SubjectCode.Trim().ToUpperInvariant();
            grades = grades.Where(g => g.SubjectCode == code);
        }

        if (!string.IsNullOrWhiteSpace(query.Semester))
        {
            var semester = query.Semester.Trim();
            grades = grades.Where(g => g.Semester == semester);
        }

        if (!string.IsNullOrWhiteSpace(query.SchoolYear))
        {
            var schoolYear = query.SchoolYear.Trim();
            grades = grades.Where(g => g.SchoolYear == schoolYear);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            if (term.Length > MaxSearchLength) term = term[..MaxSearchLength];

            grades = grades.Where(g =>
                g.SubjectCode.Contains(term) ||
                g.SubjectName.Contains(term) ||
                g.Remarks.Contains(term) ||
                g.Student!.StudentNumber.Contains(term) ||
                g.Student.FirstName.Contains(term) ||
                g.Student.LastName.Contains(term) ||
                (g.Student.FirstName + " " + g.Student.LastName).Contains(term));
        }

        return await grades
            .OrderBy(g => g.Student!.StudentNumber)
            .ThenByDescending(g => g.SchoolYear)
            .ThenBy(g => g.Semester)
            .ThenBy(g => g.SubjectCode)
            .Select(ToResponse)
            .ToListAsync(ct);
    }

    public async Task<GradeResponse?> GetByIdAsync(int gradeId, int? ownerStudentId, CancellationToken ct = default)
    {
        var grades = db.StudentGrades.AsNoTracking().Where(g => g.GradeId == gradeId);

        // Ownership filter: a student caller can only ever match their own record.
        if (ownerStudentId.HasValue)
            grades = grades.Where(g => g.StudentId == ownerStudentId.Value);

        return await grades.Select(ToResponse).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<GradeResponse>> GetForStudentAsync(int studentId, CancellationToken ct = default) =>
        await db.StudentGrades.AsNoTracking()
            .Where(g => g.StudentId == studentId)
            .OrderByDescending(g => g.SchoolYear)
            .ThenBy(g => g.Semester)
            .ThenBy(g => g.SubjectCode)
            .Select(ToResponse)
            .ToListAsync(ct);

    public async Task<ServiceResult<GradeResponse>> CreateAsync(GradeCreateRequest request, int facultyId, CancellationToken ct = default)
    {
        var input = Normalize(request);

        var facultyIsActive = await db.Faculty.AsNoTracking()
            .AnyAsync(f => f.FacultyId == facultyId && f.IsActive, ct);
        if (!facultyIsActive)
            return ServiceResult<GradeResponse>.Forbidden("Your account is not linked to an active faculty record.");

        var studentError = await ValidateStudentAsync(input.StudentId, ct);
        if (studentError is not null) return studentError;

        if (await IsDuplicateAsync(input, excludeGradeId: null, ct))
            return await DuplicateResultAsync(input, ct);

        var entity = new StudentGrade
        {
            StudentId = input.StudentId,
            SubjectCode = input.SubjectCode,
            SubjectName = input.SubjectName,
            FacultyId = facultyId,                              // from the JWT, never from the body
            Grade = input.Grade,
            Remarks = remarksCalculator.Calculate(input.Grade), // computed on the server, always
            Semester = input.Semester,
            SchoolYear = input.SchoolYear,
            DatePosted = UtcNowSeconds(),
            DateModified = null
        };

        db.StudentGrades.Add(entity);

        if (!await TrySaveAsync(ct))
            return await DuplicateResultAsync(input, ct);

        logger.LogInformation("Grade {GradeId} created by faculty {FacultyId} ({Grade} -> {Remarks}).",
            entity.GradeId, facultyId, entity.Grade, entity.Remarks);

        var created = await GetByIdAsync(entity.GradeId, null, ct);
        return ServiceResult<GradeResponse>.Success(created!);
    }

    public async Task<ServiceResult<GradeResponse>> UpdateAsync(int gradeId, GradeUpdateRequest request, CancellationToken ct = default)
    {
        var entity = await db.StudentGrades.FirstOrDefaultAsync(g => g.GradeId == gradeId, ct);
        if (entity is null)
            return ServiceResult<GradeResponse>.NotFound($"Grade record {gradeId} was not found.");

        var input = Normalize(request);

        var studentError = await ValidateStudentAsync(input.StudentId, ct);
        if (studentError is not null) return studentError;

        if (await IsDuplicateAsync(input, excludeGradeId: gradeId, ct))
            return await DuplicateResultAsync(input, ct);

        entity.StudentId = input.StudentId;
        entity.SubjectCode = input.SubjectCode;
        entity.SubjectName = input.SubjectName;
        entity.Grade = input.Grade;
        entity.Remarks = remarksCalculator.Calculate(input.Grade); // recomputed on EVERY update
        entity.Semester = input.Semester;
        entity.SchoolYear = input.SchoolYear;
        entity.DateModified = UtcNowSeconds();
        // entity.FacultyId and entity.DatePosted are intentionally untouched (original poster is kept).

        if (!await TrySaveAsync(ct))
            return await DuplicateResultAsync(input, ct);

        logger.LogInformation("Grade {GradeId} updated ({Grade} -> {Remarks}).", entity.GradeId, entity.Grade, entity.Remarks);

        var updated = await GetByIdAsync(entity.GradeId, null, ct);
        return ServiceResult<GradeResponse>.Success(updated!);
    }

    public async Task<bool> DeleteAsync(int gradeId, CancellationToken ct = default)
    {
        var entity = await db.StudentGrades.FirstOrDefaultAsync(g => g.GradeId == gradeId, ct);
        if (entity is null) return false;

        db.StudentGrades.Remove(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Grade {GradeId} deleted.", gradeId);
        return true;
    }

    // ---------------------------------------------------------------- helpers

    private sealed record NormalizedGrade(
        int StudentId, string SubjectCode, string SubjectName, int Grade, string Semester, string SchoolYear);

    /// <summary>Trims input and upper-cases the subject code. DTO validation has already guaranteed non-null values.</summary>
    private static NormalizedGrade Normalize(GradeWriteRequest r) => new(
        r.StudentId ?? throw new ArgumentException("StudentId is required.", nameof(r)),
        (r.SubjectCode ?? string.Empty).Trim().ToUpperInvariant(),
        (r.SubjectName ?? string.Empty).Trim(),
        r.Grade ?? throw new ArgumentException("Grade is required.", nameof(r)),
        (r.Semester ?? string.Empty).Trim(),
        (r.SchoolYear ?? string.Empty).Trim());

    private async Task<ServiceResult<GradeResponse>?> ValidateStudentAsync(int studentId, CancellationToken ct)
    {
        var student = await db.Students.AsNoTracking()
            .Where(s => s.StudentId == studentId)
            .Select(s => new { s.IsActive })
            .FirstOrDefaultAsync(ct);

        if (student is null)
            return ServiceResult<GradeResponse>.Invalid("studentId", $"Student {studentId} does not exist.");
        if (!student.IsActive)
            return ServiceResult<GradeResponse>.Invalid("studentId", "The selected student is inactive and cannot receive grades.");
        return null;
    }

    private Task<bool> IsDuplicateAsync(NormalizedGrade input, int? excludeGradeId, CancellationToken ct) =>
        db.StudentGrades.AsNoTracking().AnyAsync(g =>
            g.StudentId == input.StudentId &&
            g.SubjectCode == input.SubjectCode &&
            g.Semester == input.Semester &&
            g.SchoolYear == input.SchoolYear &&
            (excludeGradeId == null || g.GradeId != excludeGradeId), ct);

    private async Task<ServiceResult<GradeResponse>> DuplicateResultAsync(NormalizedGrade input, CancellationToken ct)
    {
        var studentNumber = await db.Students.AsNoTracking()
            .Where(s => s.StudentId == input.StudentId)
            .Select(s => s.StudentNumber)
            .FirstOrDefaultAsync(ct) ?? input.StudentId.ToString();

        return ServiceResult<GradeResponse>.Conflict(
            $"Student {studentNumber} already has a grade for {input.SubjectCode} in {input.Semester}, " +
            $"school year {input.SchoolYear}. Edit the existing record instead of adding a new one.");
    }

    /// <summary>Saves changes; returns false when SQL Server rejects the row with the unique constraint.</summary>
    private async Task<bool> TrySaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            // Race condition: another request inserted the same Student/Subject/Term between our check and save.
            logger.LogWarning("Unique constraint UQ_StudentGrades_StudentSubjectTerm rejected a grade write.");
            db.ChangeTracker.Clear();
            return false;
        }
    }

    internal static bool IsUniqueConstraintViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2627 or 2601 };

    /// <summary>UTC now truncated to whole seconds (the columns are DATETIME2(0)).</summary>
    private DateTime UtcNowSeconds()
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        return new DateTime(now.Ticks - now.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);
    }
}
