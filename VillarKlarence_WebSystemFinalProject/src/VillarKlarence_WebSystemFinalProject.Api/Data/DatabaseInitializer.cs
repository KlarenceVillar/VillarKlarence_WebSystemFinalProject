using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace VillarKlarence_WebSystemFinalProject.Api.Data;

/// <summary>
/// Runs at API startup: verifies the instructor's SQL script was run, applies EF migrations
/// (Identity tables only) and seeds the Identity roles/demo accounts.
/// </summary>
public static class DatabaseInitializer
{
    public const string SetupHint =
        "Run Database/StudentGradeManagementDB.sql first (SSMS or sqlcmd), then start the API again.";

    public static async Task InitializeAsync(IServiceProvider rootServices, CancellationToken ct = default)
    {
        await using var scope = rootServices.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (db.Database.IsRelational())
        {
            if (!await db.Database.CanConnectAsync(ct))
                throw new InvalidOperationException(
                    "Cannot connect to the StudentGradeManagementDB database. Check ConnectionStrings:DefaultConnection. " + SetupHint);

            try
            {
                // LINQ probe: fails with SQL error 208 when dbo.Students does not exist yet.
                await db.Students.AsNoTracking().AnyAsync(ct);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                throw new InvalidOperationException("The academic tables are missing. " + SetupHint, ex);
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
            if (pending.Count > 0)
            {
                logger.LogInformation("Applying EF Core migrations: {Migrations}", string.Join(", ", pending));
                await db.Database.MigrateAsync(ct);
            }
        }

        await IdentitySeeder.SeedAsync(services, ct);
        logger.LogInformation("Database ready.");
    }
}
