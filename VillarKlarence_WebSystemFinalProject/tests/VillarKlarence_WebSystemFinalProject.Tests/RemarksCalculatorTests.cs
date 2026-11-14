using VillarKlarence_WebSystemFinalProject.Api.Services;

namespace VillarKlarence_WebSystemFinalProject.Tests;

/// <summary>Every boundary of the instructor's dbo.GetGradeRemarks procedure.</summary>
public class RemarksCalculatorTests
{
    private readonly RemarksCalculator _calculator = new();

    [Theory]
    [InlineData(0, "BEP/FAILED")]
    [InlineData(74, "BEP/FAILED")]
    [InlineData(75, "EP")]
    [InlineData(79, "EP")]
    [InlineData(80, "GP")]
    [InlineData(84, "GP")]
    [InlineData(85, "SAP")]
    [InlineData(89, "SAP")]
    [InlineData(90, "SP")]
    [InlineData(94, "SP")]
    [InlineData(95, "CMS")]
    [InlineData(100, "CMS")]
    public void Calculate_ReturnsRemark_AtEveryBoundary(int grade, string expected) =>
        Assert.Equal(expected, _calculator.Calculate(grade));

    [Theory]
    [InlineData(1, "BEP/FAILED")]
    [InlineData(50, "BEP/FAILED")]
    [InlineData(77, "EP")]
    [InlineData(82, "GP")]
    [InlineData(87, "SAP")]
    [InlineData(92, "SP")]
    [InlineData(98, "CMS")]
    public void Calculate_ReturnsRemark_InsideRanges(int grade, string expected) =>
        Assert.Equal(expected, _calculator.Calculate(grade));

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(-100)]
    [InlineData(1000)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void Calculate_Throws_WhenGradeOutOfRange(int grade)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _calculator.Calculate(grade));
        Assert.Contains("Grade must be a whole number from 0 to 100.", ex.Message);
    }

    [Fact]
    public void Calculate_CoversEveryGradeFrom0To100_WithOneOfTheSixRemarks()
    {
        string[] allowed = ["CMS", "SP", "SAP", "GP", "EP", "BEP/FAILED"];
        for (var grade = 0; grade <= 100; grade++)
            Assert.Contains(_calculator.Calculate(grade), allowed);
    }
}
