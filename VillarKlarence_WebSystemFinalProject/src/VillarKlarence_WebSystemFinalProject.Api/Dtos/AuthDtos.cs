using System.ComponentModel.DataAnnotations;

namespace VillarKlarence_WebSystemFinalProject.Api.Dtos;

/// <summary>POST /api/auth/login body.</summary>
public sealed class LoginRequest
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(256)]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128)]
    public string? Password { get; set; }
}

/// <summary>The signed-in user's profile (also returned by GET /api/auth/me).</summary>
public sealed record UserInfoResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public int? StudentId { get; init; }
    public string? StudentNumber { get; init; }
    public int? FacultyId { get; init; }
    public string? FacultyNumber { get; init; }
}

/// <summary>POST /api/auth/login response.</summary>
public sealed record LoginResponse
{
    public string Token { get; init; } = string.Empty;
    public string TokenType { get; init; } = "Bearer";
    public DateTime ExpiresAtUtc { get; init; }
    public UserInfoResponse User { get; init; } = new();
}
