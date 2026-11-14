using System.ComponentModel.DataAnnotations;

namespace VillarKlarence_WebSystemFinalProject.Api.Security;

/// <summary>
/// Bound from the "Jwt" configuration section. Issuer/Audience live in appsettings.json;
/// Key comes from dotnet user-secrets (setup-secrets.cmd) and is never committed.
/// </summary>
public class JwtSettings
{
    public const string SectionName = "Jwt";

    [Required(ErrorMessage = "Jwt:Key is missing. Run setup-secrets.cmd (dotnet user-secrets) before starting the API.")]
    [MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters long.")]
    public string Key { get; set; } = string.Empty;

    [Required]
    public string Issuer { get; set; } = string.Empty;

    [Required]
    public string Audience { get; set; } = string.Empty;

    [Range(5, 1440)]
    public int ExpiresMinutes { get; set; } = 120;
}
