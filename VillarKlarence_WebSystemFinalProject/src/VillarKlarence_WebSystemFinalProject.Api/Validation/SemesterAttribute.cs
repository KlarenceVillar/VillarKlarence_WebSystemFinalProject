using System.ComponentModel.DataAnnotations;

namespace VillarKlarence_WebSystemFinalProject.Api.Validation;

/// <summary>Accepts only "1st Semester", "2nd Semester" or "Summer". Null is left to [Required].</summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class SemesterAttribute : ValidationAttribute
{
    public SemesterAttribute() : base(GradeRules.SemesterMessage) { }

    public override bool IsValid(object? value) =>
        value is null || (value is string s && GradeRules.Semesters.Contains(s.Trim(), StringComparer.Ordinal));
}
