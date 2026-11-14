using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using VillarKlarence_WebSystemFinalProject.Api.Models;
using VillarKlarence_WebSystemFinalProject.Api.Security;

namespace VillarKlarence_WebSystemFinalProject.Api.Data;

/// <summary>
/// Creates the "Faculty" and "Student" roles and the demo Identity accounts (only when missing),
/// linked to the seed rows inserted by Database/StudentGradeManagementDB.sql.
/// The demo password is read from configuration "Seed:DemoPassword" (user-secrets), never hard-coded.
/// </summary>
public static class IdentitySeeder
{
    private sealed record DemoAccount(string Email, string Role, string ProfileNumber);

    private static readonly DemoAccount[] DemoAccounts =
    [
        new("andrea.garcia@example.com", AppRoles.Faculty, "FAC-001"),
        new("michael.ramos@example.com", AppRoles.Faculty, "FAC-002"),
        new("juan.delacruz@example.com", AppRoles.Student, "2026-0001"),
        new("maria.santos@example.com", AppRoles.Student, "2026-0002"),
        new("pedro.reyes@example.com", AppRoles.Student, "2026-0003")
    ];

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(IdentitySeeder));
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var configuration = services.GetRequiredService<IConfiguration>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                ThrowIfFailed(result, $"create role {role}");
                logger.LogInformation("Created role {Role}.", role);
            }
        }

        var demoPassword = configuration["Seed:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
        {
            logger.LogWarning("Seed:DemoPassword is not configured (run setup-secrets.cmd). Demo accounts were not created.");
            return;
        }

        foreach (var account in DemoAccounts)
        {
            int? studentId = null, facultyId = null;
            if (account.Role == AppRoles.Student)
            {
                studentId = await db.Students.AsNoTracking()
                    .Where(s => s.StudentNumber == account.ProfileNumber)
                    .Select(s => (int?)s.StudentId)
                    .FirstOrDefaultAsync(ct);
            }
            else
            {
                facultyId = await db.Faculty.AsNoTracking()
                    .Where(f => f.FacultyNumber == account.ProfileNumber)
                    .Select(f => (int?)f.FacultyId)
                    .FirstOrDefaultAsync(ct);
            }

            if (studentId is null && facultyId is null)
            {
                logger.LogWarning("Skipping {Email}: {Number} was not found. Did you run the SQL script?",
                    account.Email, account.ProfileNumber);
                continue;
            }

            var user = await userManager.FindByEmailAsync(account.Email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    UserName = account.Email,
                    Email = account.Email,
                    EmailConfirmed = true,
                    StudentId = studentId,
                    FacultyId = facultyId
                };
                EnsureValid(user, account.Role);

                ThrowIfFailed(await userManager.CreateAsync(user, demoPassword), $"create user {account.Email}");
                logger.LogInformation("Created {Role} account {Email}.", account.Role, account.Email);
            }
            else if (user.StudentId != studentId || user.FacultyId != facultyId)
            {
                // Repair links so the Student/Faculty rule always holds (password is left untouched).
                user.StudentId = studentId;
                user.FacultyId = facultyId;
                EnsureValid(user, account.Role);
                ThrowIfFailed(await userManager.UpdateAsync(user), $"update links for {account.Email}");
                logger.LogInformation("Repaired profile link for {Email}.", account.Email);
            }

            var currentRoles = await userManager.GetRolesAsync(user);
            var wrongRoles = currentRoles.Where(r => r != account.Role).ToList();
            if (wrongRoles.Count > 0)
                ThrowIfFailed(await userManager.RemoveFromRolesAsync(user, wrongRoles), $"remove roles from {account.Email}");
            if (!currentRoles.Contains(account.Role))
                ThrowIfFailed(await userManager.AddToRoleAsync(user, account.Role), $"add {account.Email} to {account.Role}");
        }
    }

    private static void EnsureValid(ApplicationUser user, string role)
    {
        var problem = UserAccountRules.Validate(user, role);
        if (problem is not null)
            throw new InvalidOperationException($"Invalid seed account {user.Email}: {problem}");
    }

    private static void ThrowIfFailed(IdentityResult result, string action)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(
                $"Identity seeding failed to {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}");
    }
}
