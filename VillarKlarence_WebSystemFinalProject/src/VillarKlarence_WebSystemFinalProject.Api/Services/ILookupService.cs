using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public interface ILookupService
{
    Task<IReadOnlyList<StudentOptionResponse>> GetActiveStudentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<SubjectOptionResponse>> GetSubjectsAsync(CancellationToken ct = default);
}
