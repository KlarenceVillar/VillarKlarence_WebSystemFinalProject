using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public interface IGradeService
{
    /// <summary>All grade records (faculty view) with optional filters.</summary>
    Task<IReadOnlyList<GradeResponse>> GetAllAsync(GradeQuery query, CancellationToken ct = default);

    /// <summary>
    /// One grade record. When <paramref name="ownerStudentId"/> is set (student caller) the record is
    /// returned only if it belongs to that student; otherwise null (the controller answers 404).
    /// </summary>
    Task<GradeResponse?> GetByIdAsync(int gradeId, int? ownerStudentId, CancellationToken ct = default);

    /// <summary>Grades of exactly one student (the id comes from the caller's "student_id" claim).</summary>
    Task<IReadOnlyList<GradeResponse>> GetForStudentAsync(int studentId, CancellationToken ct = default);

    /// <summary>Creates a grade. Remarks are computed here; FacultyId comes from the caller's claims.</summary>
    Task<ServiceResult<GradeResponse>> CreateAsync(GradeCreateRequest request, int facultyId, CancellationToken ct = default);

    /// <summary>Updates a grade. Remarks are recomputed; the original poster (FacultyId) is kept.</summary>
    Task<ServiceResult<GradeResponse>> UpdateAsync(int gradeId, GradeUpdateRequest request, CancellationToken ct = default);

    /// <summary>Deletes a grade. Returns false when it does not exist.</summary>
    Task<bool> DeleteAsync(int gradeId, CancellationToken ct = default);
}
