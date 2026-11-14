namespace VillarKlarence_WebSystemFinalProject.Api.Services;

/// <summary>Computes the official remark for a numerical grade (C# equivalent of dbo.GetGradeRemarks).</summary>
public interface IRemarksCalculator
{
    /// <exception cref="ArgumentOutOfRangeException">Grade is below 0 or above 100.</exception>
    string Calculate(int grade);
}
