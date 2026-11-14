namespace VillarKlarence_WebSystemFinalProject.Api.Validation;

/// <summary>Single source of truth for grade validation rules and messages.</summary>
public static class GradeRules
{
    public const int MinGrade = 0;
    public const int MaxGrade = 100;

    public const string GradeMessage = "Grade must be a whole number from 0 to 100.";
    public const string GradeRequiredMessage = "Grade is required. " + GradeMessage;

    public static readonly string[] Semesters = ["1st Semester", "2nd Semester", "Summer"];
    public const string SemesterMessage = "Semester must be one of: 1st Semester, 2nd Semester, Summer.";

    public const string SchoolYearMessage =
        "School year must use the format YYYY-YYYY with consecutive years (for example 2026-2027).";
}
