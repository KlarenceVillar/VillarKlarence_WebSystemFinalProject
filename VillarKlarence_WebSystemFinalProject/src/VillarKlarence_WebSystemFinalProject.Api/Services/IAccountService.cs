using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public enum LoginStatus
{
    Success,
    InvalidCredentials,
    LockedOut,
    AccountNotLinked
}

public sealed record LoginResult(LoginStatus Status, LoginResponse? Response = null, string? Message = null);

public interface IAccountService
{
    /// <summary>Checks the password with ASP.NET Core Identity and, on success, issues a JWT.</summary>
    Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default);

    /// <summary>Profile of the user identified by the NameIdentifier claim.</summary>
    Task<UserInfoResponse?> GetProfileAsync(string userId, CancellationToken ct = default);
}
