using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Dtos;
using VillarKlarence_WebSystemFinalProject.Api.Models;
using VillarKlarence_WebSystemFinalProject.Api.Security;

namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public sealed class AccountService(
    UserManager<ApplicationUser> userManager,
    AppDbContext db,
    ITokenService tokenService,
    ILogger<AccountService> logger) : IAccountService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    public async Task<LoginResult> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
            return new LoginResult(LoginStatus.InvalidCredentials, Message: InvalidCredentialsMessage);

        if (await userManager.IsLockedOutAsync(user))
            return new LoginResult(LoginStatus.LockedOut,
                Message: "Too many failed sign-in attempts. Please try again in a few minutes.");

        // Identity compares against the stored PBKDF2 password hash; no plaintext passwords exist anywhere.
        if (!await userManager.CheckPasswordAsync(user, password))
        {
            await userManager.AccessFailedAsync(user);
            logger.LogWarning("Failed sign-in for user {UserId}.", user.Id);
            return new LoginResult(LoginStatus.InvalidCredentials, Message: InvalidCredentialsMessage);
        }

        await userManager.ResetAccessFailedCountAsync(user);

        var profile = await BuildProfileAsync(user, ct);
        if (profile is null)
        {
            logger.LogWarning("User {UserId} has a valid password but no valid role/profile link.", user.Id);
            return new LoginResult(LoginStatus.AccountNotLinked,
                Message: "This account is not linked to an active student or faculty record. Please contact the registrar.");
        }

        var (token, expires) = tokenService.CreateToken(profile);
        logger.LogInformation("User {UserId} signed in as {Role}.", user.Id, profile.Role);

        return new LoginResult(LoginStatus.Success, new LoginResponse
        {
            Token = token,
            ExpiresAtUtc = expires,
            User = profile
        });
    }

    public async Task<UserInfoResponse?> GetProfileAsync(string userId, CancellationToken ct = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        return user is null ? null : await BuildProfileAsync(user, ct);
    }

    /// <summary>Returns null unless the account has exactly one valid role and an active linked record.</summary>
    private async Task<UserInfoResponse?> BuildProfileAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = (await userManager.GetRolesAsync(user)).Where(r => AppRoles.All.Contains(r)).ToList();
        if (roles.Count != 1) return null;

        var role = roles[0];
        if (UserAccountRules.Validate(user, role) is not null) return null;

        if (role == AppRoles.Student)
        {
            var student = await db.Students.AsNoTracking()
                .FirstOrDefaultAsync(s => s.StudentId == user.StudentId && s.IsActive, ct);
            if (student is null) return null;

            return new UserInfoResponse
            {
                UserId = user.Id,
                Email = user.Email ?? student.Email,
                FullName = $"{student.FirstName} {student.LastName}",
                Role = role,
                StudentId = student.StudentId,
                StudentNumber = student.StudentNumber
            };
        }

        var faculty = await db.Faculty.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FacultyId == user.FacultyId && f.IsActive, ct);
        if (faculty is null) return null;

        return new UserInfoResponse
        {
            UserId = user.Id,
            Email = user.Email ?? faculty.Email,
            FullName = $"{faculty.FirstName} {faculty.LastName}",
            Role = role,
            FacultyId = faculty.FacultyId,
            FacultyNumber = faculty.FacultyNumber
        };
    }
}
