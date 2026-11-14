using System.ComponentModel.DataAnnotations;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Tests;

/// <summary>DataAnnotations rules on the create/update DTOs (what [ApiController] checks before the action runs).</summary>
public class GradeRequestValidationTests
{
    private static GradeCreateRequest Valid() => new()
    {
        StudentId = 1,
        SubjectCode = "MTH101",
        SubjectName = "College Algebra",
        Grade = 88,
        Semester = "1st Semester",
        SchoolYear = "2026-2027"
    };

    private static List<ValidationResult> Validate(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }

    private static void AssertInvalid(GradeCreateRequest request, string member, string? messageFragment = null)
    {
        var errors = Validate(request);
        var error = Assert.Single(errors, e => e.MemberNames.Contains(member));
        if (messageFragment is not null) Assert.Contains(messageFragment, error.ErrorMessage);
    }

    [Fact]
    public void ValidRequest_HasNoErrors() => Assert.Empty(Validate(Valid()));

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Grade_BoundaryValues_AreAccepted(int grade)
    {
        var r = Valid(); r.Grade = grade;
        Assert.Empty(Validate(r));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(105)]
    public void Grade_OutOfRange_IsRejected(int grade)
    {
        var r = Valid(); r.Grade = grade;
        AssertInvalid(r, nameof(GradeCreateRequest.Grade), "Grade must be a whole number from 0 to 100.");
    }

    [Fact]
    public void Grade_Missing_IsRejected()
    {
        var r = Valid(); r.Grade = null;
        AssertInvalid(r, nameof(GradeCreateRequest.Grade), "Grade is required.");
    }

    [Theory]
    [InlineData("1st semester")]
    [InlineData("Third Semester")]
    [InlineData("")]
    public void Semester_NotInAllowedList_IsRejected(string semester)
    {
        var r = Valid(); r.Semester = semester;
        Assert.Contains(Validate(r), e => e.MemberNames.Contains(nameof(GradeCreateRequest.Semester)));
    }

    [Theory]
    [InlineData("1st Semester")]
    [InlineData("2nd Semester")]
    [InlineData("Summer")]
    public void Semester_AllowedValues_AreAccepted(string semester)
    {
        var r = Valid(); r.Semester = semester;
        Assert.Empty(Validate(r));
    }

    [Theory]
    [InlineData("2026-2028")]
    [InlineData("2027-2026")]
    [InlineData("2026/2027")]
    [InlineData("26-27")]
    [InlineData("abcd-efgh")]
    public void SchoolYear_BadFormatOrNonConsecutive_IsRejected(string schoolYear)
    {
        var r = Valid(); r.SchoolYear = schoolYear;
        AssertInvalid(r, nameof(GradeCreateRequest.SchoolYear), "YYYY-YYYY");
    }

    [Fact]
    public void RequiredText_WhitespaceOnly_IsRejected()
    {
        var r = Valid(); r.SubjectCode = "   "; r.SubjectName = " ";
        var errors = Validate(r);
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(GradeCreateRequest.SubjectCode)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(GradeCreateRequest.SubjectName)));
    }

    [Fact]
    public void SubjectCode_TooLong_IsRejected()
    {
        var r = Valid(); r.SubjectCode = new string('A', 31);
        AssertInvalid(r, nameof(GradeCreateRequest.SubjectCode));
    }

    [Theory]
    [InlineData(typeof(GradeCreateRequest))]
    [InlineData(typeof(GradeUpdateRequest))]
    public void WriteDtos_HaveNoRemarksOrFacultyIdProperty(Type dtoType)
    {
        Assert.Null(dtoType.GetProperty("Remarks"));
        Assert.Null(dtoType.GetProperty("FacultyId"));
    }
}
