using VillarKlarence_WebSystemFinalProject.Api.Validation;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

/// <summary>
/// The ONLY place where remarks are decided. Mirrors the instructor's stored procedure
/// dbo.GetGradeRemarks exactly:
///   95-100 CMS | 90-94 SP | 85-89 SAP | 80-84 GP | 75-79 EP | 0-74 BEP/FAILED
/// </summary>
public sealed class RemarksCalculator : IRemarksCalculator
{
    public const string Cms = "CMS";
    public const string Sp = "SP";
    public const string Sap = "SAP";
    public const string Gp = "GP";
    public const string Ep = "EP";
    public const string Failed = "BEP/FAILED";

    public string Calculate(int grade)
    {
        if (grade < GradeRules.MinGrade || grade > GradeRules.MaxGrade)
        {
            // Same rule (and message) as THROW 50001 in dbo.GetGradeRemarks.
            throw new ArgumentOutOfRangeException(nameof(grade), grade, GradeRules.GradeMessage);
        }

        return grade switch
        {
            >= 95 => Cms,     // 95-100
            >= 90 => Sp,      // 90-94
            >= 85 => Sap,     // 85-89
            >= 80 => Gp,      // 80-84
            >= 75 => Ep,      // 75-79
            _ => Failed       // 0-74
        };
    }
}
