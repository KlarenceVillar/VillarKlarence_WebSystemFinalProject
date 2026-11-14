using VillarKlarence_WebSystemFinalProject.Api.Dtos;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public interface ITokenService
{
    /// <summary>Creates a signed JWT for an already-authenticated user.</summary>
    (string Token, DateTime ExpiresAtUtc) CreateToken(UserInfoResponse user);
}
