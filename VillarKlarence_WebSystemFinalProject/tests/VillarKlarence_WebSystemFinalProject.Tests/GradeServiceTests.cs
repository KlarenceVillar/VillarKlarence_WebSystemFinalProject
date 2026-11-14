using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Services;
using VillarKlarence_WebSystemFinalProject.Tests.Infrastructure;

namespace VillarKlarence_WebSystemFinalProject.Tests;

/// <summary>GradeService business rules against the EF Core InMemory provider.</summary>
public sealed class GradeServiceTests : IDisposable
{
    private readonly AppDbContext _db = TestData.CreateInMemoryContext();
    private readonly ManualTimeProvider _clock = new(new DateTimeOffset(2026, 11, 14, 9, 30, 15, 123, TimeSpan.Zero));
    private readonly GradeService _service;

    public GradeServiceTests()
    {
        _service = new GradeService(_db, new RemarksCalculator(), _clock, NullLogger<GradeService>.Instance);
    }

    public void Dispose() => _db.Dispose();

    private static GradeCreateRequest NewGrade(int grade, int studentId = TestData.PedroId, string code = "MTH101") => new()
    {
        StudentId = studentId,
        SubjectCode = code,
        SubjectName = "College Algebra",
        Grade = grade,
        Semester = "1st Semester",
        SchoolYear = "2026-2027"
    };

    private static GradeUpdateRequest UpdateFrom(GradeResponse r, int grade) => new()
    {
        StudentId = r.StudentId,
        SubjectCode = r.SubjectCode,
        SubjectName = r.SubjectName,
        Grade = grade,
        Semester = r.Semester,
        SchoolYear = r.SchoolYear
    };

    // ------------------------------------------------------------------ create

    [Theory]
    [InlineData(100, "CMS")]
    [InlineData(91, "SP")]
    [InlineData(85, "SAP")]
    [InlineData(84, "GP")]
    [InlineData(78, "EP")]
    [InlineData(70, "BEP/FAILED")]
    public async Task Create_ComputesRemarkOnServer(int grade, string expected)
    {
        var result = await _service.CreateAsync(NewGrade(grade), TestData.AndreaId);

        Assert.True(result.Succeeded);
        Assert.Equal(expected, result.Value!.Remarks);
        Assert.Equal(expected, _db.StudentGrades.Single(g => g.GradeId == result.Value.GradeId).Remarks);
    }

    [Fact]
    public async Task Create_IgnoresClientSuppliedRemarksAndFacultyId()
    {
        // A tampered request body: the client claims "CMS" and tries to post as faculty 2.
        const string json = """
            {"studentId":3,"subjectCode":"MTH101","subjectName":"College Algebra","grade":70,
             "remarks":"CMS","facultyId":2,"semester":"1st Semester","schoolYear":"2026-2027"}
            """;
        var request = JsonSerializer.Deserialize<GradeCreateRequest>(json, JsonSerializerOptions.Web)!;

        var result = await _service.CreateAsync(request, facultyId: TestData.AndreaId);

        Assert.True(result.Succeeded);
        Assert.Equal("BEP/FAILED", result.Value!.Remarks);
        Assert.Equal(TestData.AndreaId, result.Value.FacultyId);
    }

    [Fact]
    public async Task Create_SetsDatePostedToUtcNow_AndNoDateModified()
    {
        var result = await _service.CreateAsync(NewGrade(88), TestData.MichaelId);

        Assert.Equal(new DateTime(2026, 11, 14, 9, 30, 15, DateTimeKind.Utc), result.Value!.DatePosted);
        Assert.Null(result.Value.DateModified);
        Assert.Equal("Michael Ramos", result.Value.FacultyName);
    }

    [Fact]
    public async Task Create_NormalizesSubjectCodeToUpperCase()
    {
        var result = await _service.CreateAsync(NewGrade(88, code: " mth101 "), TestData.AndreaId);
        Assert.Equal("MTH101", result.Value!.SubjectCode);
    }

    [Fact]
    public async Task Create_Duplicate_ReturnsConflict()
    {
        // Juan already has WEB101 / 1st Semester / 2026-2027 in the seed data (lower-case on purpose).
        var result = await _service.CreateAsync(NewGrade(90, TestData.JuanId, "web101"), TestData.AndreaId);

        Assert.Equal(ServiceStatus.Conflict, result.Status);
        Assert.Contains("already has a grade for WEB101", result.Message);
        Assert.Equal(2, _db.StudentGrades.Count(g => g.StudentId == TestData.JuanId));
    }

    [Fact]
    public async Task Create_UnknownStudent_IsValidationError()
    {
        var result = await _service.CreateAsync(NewGrade(90, studentId: 999), TestData.AndreaId);

        Assert.Equal(ServiceStatus.ValidationFailed, result.Status);
        Assert.True(result.Errors.ContainsKey("studentId"));
    }

    [Fact]
    public async Task Create_InactiveStudent_IsValidationError()
    {
        var result = await _service.CreateAsync(NewGrade(90, studentId: TestData.InactiveStudentId), TestData.AndreaId);

        Assert.Equal(ServiceStatus.ValidationFailed, result.Status);
        Assert.Contains("inactive", result.Message);
    }

    [Fact]
    public async Task Create_UnknownFaculty_IsForbidden()
    {
        var result = await _service.CreateAsync(NewGrade(90), facultyId: 999);
        Assert.Equal(ServiceStatus.Forbidden, result.Status);
    }

    // ------------------------------------------------------------------ update

    [Fact]
    public async Task Update_RecomputesRemark_EpToCms()
    {
        var created = (await _service.CreateAsync(NewGrade(78), TestData.AndreaId)).Value!;
        Assert.Equal("EP", created.Remarks);

        _clock.Advance(TimeSpan.FromMinutes(10));
        var updated = await _service.UpdateAsync(created.GradeId, UpdateFrom(created, 96));

        Assert.True(updated.Succeeded);
        Assert.Equal(96, updated.Value!.Grade);
        Assert.Equal("CMS", updated.Value.Remarks);
    }

    [Fact]
    public async Task Update_IgnoresClientRemarks()
    {
        var created = (await _service.CreateAsync(NewGrade(70), TestData.AndreaId)).Value!;
        var json = $$"""
            {"studentId":{{created.StudentId}},"subjectCode":"{{created.SubjectCode}}","subjectName":"{{created.SubjectName}}",
             "grade":91,"remarks":"BEP/FAILED","semester":"{{created.Semester}}","schoolYear":"{{created.SchoolYear}}"}
            """;
        var request = JsonSerializer.Deserialize<GradeUpdateRequest>(json, JsonSerializerOptions.Web)!;

        var updated = await _service.UpdateAsync(created.GradeId, request);

        Assert.Equal("SP", updated.Value!.Remarks);
    }

    [Fact]
    public async Task Update_KeepsOriginalPosterAndDatePosted_SetsDateModified()
    {
        var created = (await _service.CreateAsync(NewGrade(80), TestData.MichaelId)).Value!;
        _clock.Advance(TimeSpan.FromHours(2));

        var updated = (await _service.UpdateAsync(created.GradeId, UpdateFrom(created, 81))).Value!;

        Assert.Equal(TestData.MichaelId, updated.FacultyId);
        Assert.Equal(created.DatePosted, updated.DatePosted);
        Assert.Equal(new DateTime(2026, 11, 14, 11, 30, 15, DateTimeKind.Utc), updated.DateModified);
    }

    [Fact]
    public async Task Update_SeededRecord_NeverKeepsOldRemark()
    {
        var juanWeb = (await _service.GetByIdAsync(TestData.JuanWeb101GradeId, null))!;
        Assert.Equal("CMS", juanWeb.Remarks);

        var updated = await _service.UpdateAsync(juanWeb.GradeId, UpdateFrom(juanWeb, 60));

        Assert.Equal("BEP/FAILED", updated.Value!.Remarks);
    }

    [Fact]
    public async Task Update_IntoExistingStudentSubjectTerm_ReturnsConflict()
    {
        var juanNet = (await _service.GetByIdAsync(2, null))!; // Juan NET101
        var request = UpdateFrom(juanNet, 90);
        request.SubjectCode = "WEB101"; // Juan already has WEB101 in the same term

        var result = await _service.UpdateAsync(juanNet.GradeId, request);

        Assert.Equal(ServiceStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task Update_SameRecordWithoutChangingKey_IsNotADuplicate()
    {
        var juanWeb = (await _service.GetByIdAsync(TestData.JuanWeb101GradeId, null))!;
        var result = await _service.UpdateAsync(juanWeb.GradeId, UpdateFrom(juanWeb, 97));
        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Update_MissingRecord_ReturnsNotFound()
    {
        var any = (await _service.GetByIdAsync(1, null))!;
        var result = await _service.UpdateAsync(9999, UpdateFrom(any, 90));
        Assert.Equal(ServiceStatus.NotFound, result.Status);
    }

    // ------------------------------------------------------------------ ownership / reads

    [Fact]
    public async Task GetForStudent_ReturnsOnlyThatStudentsGrades()
    {
        var grades = await _service.GetForStudentAsync(TestData.JuanId);

        Assert.Equal(2, grades.Count);
        Assert.All(grades, g => Assert.Equal(TestData.JuanId, g.StudentId));
        Assert.All(grades, g => Assert.Equal("2026-0001", g.StudentNumber));
    }

    [Fact]
    public async Task GetById_StudentOwnerFilter_HidesOtherStudentsRecord()
    {
        Assert.Null(await _service.GetByIdAsync(TestData.MariaWeb101GradeId, ownerStudentId: TestData.JuanId));
        Assert.NotNull(await _service.GetByIdAsync(TestData.JuanWeb101GradeId, ownerStudentId: TestData.JuanId));
        Assert.NotNull(await _service.GetByIdAsync(TestData.MariaWeb101GradeId, ownerStudentId: null)); // faculty
    }

    [Fact]
    public async Task GetAll_AppliesFilters()
    {
        Assert.Equal(6, (await _service.GetAllAsync(new GradeQuery())).Count);
        Assert.Equal(3, (await _service.GetAllAsync(new GradeQuery { SubjectCode = "net101" })).Count);
        Assert.Equal(2, (await _service.GetAllAsync(new GradeQuery { StudentId = TestData.MariaId })).Count);
        Assert.Equal(2, (await _service.GetAllAsync(new GradeQuery { Search = "Maria" })).Count);
        Assert.Empty(await _service.GetAllAsync(new GradeQuery { Semester = "Summer" }));
        Assert.Equal(6, (await _service.GetAllAsync(new GradeQuery { SchoolYear = "2026-2027" })).Count);
    }

    // ------------------------------------------------------------------ delete

    [Fact]
    public async Task Delete_RemovesRecord_ThenReturnsFalse()
    {
        Assert.True(await _service.DeleteAsync(TestData.MariaWeb101GradeId));
        Assert.Null(await _service.GetByIdAsync(TestData.MariaWeb101GradeId, null));
        Assert.False(await _service.DeleteAsync(TestData.MariaWeb101GradeId));
    }
}
