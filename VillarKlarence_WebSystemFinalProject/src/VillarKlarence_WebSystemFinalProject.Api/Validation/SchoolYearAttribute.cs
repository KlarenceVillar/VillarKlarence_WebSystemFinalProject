using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VillarKlarence_WebSystemFinalProject.Api.Validation;

/// <summary>
/// Accepts "YYYY-YYYY" where the second year is exactly the first year + 1 (e.g. 2026-2027).
/// Null is left to [Required].
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed partial class SchoolYearAttribute : ValidationAttribute
{
    public SchoolYearAttribute() : base(GradeRules.SchoolYearMessage) { }

    public override bool IsValid(object? value)
    {
        if (value is null) return true;
        if (value is not string text) return false;
        return IsValidSchoolYear(text.Trim());
    }

    public static bool IsValidSchoolYear(string text)
    {
        var match = SchoolYearPattern().Match(text);
        if (!match.Success) return false;

        var start = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var end = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
        return start is >= 2000 and <= 2099 && end == start + 1;
    }

    [GeneratedRegex(@"^(\d{4})-(\d{4})$", RegexOptions.CultureInvariant)]
    private static partial Regex SchoolYearPattern();
}
