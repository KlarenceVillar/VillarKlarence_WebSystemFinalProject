using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

/// <summary>Read-only lists used by the faculty grade form.</summary>
public sealed class LookupService(AppDbContext db) : ILookupService
{
    public async Task<IReadOnlyList<StudentOptionResponse>> GetActiveStudentsAsync(CancellationToken ct = default) =>
        await db.Students.AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.LastName).ThenBy(s => s.FirstName)
            .Select(s => new StudentOptionResponse(s.StudentId, s.StudentNumber, s.FirstName + " " + s.LastName, s.Email))
            .ToListAsync(ct);

    /// <summary>Distinct SubjectCode / SubjectName pairs already used in dbo.StudentGrades.</summary>
    public async Task<IReadOnlyList<SubjectOptionResponse>> GetSubjectsAsync(CancellationToken ct = default)
    {
        var pairs = await db.StudentGrades.AsNoTracking()
            .Select(g => new { g.SubjectCode, g.SubjectName })
            .Distinct()
            .ToListAsync(ct);

        return pairs
            .OrderBy(p => p.SubjectCode, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.SubjectName, StringComparer.OrdinalIgnoreCase)
            .Select(p => new SubjectOptionResponse(p.SubjectCode, p.SubjectName))
            .ToList();
    }
}
