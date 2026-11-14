using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VillarKlarence_WebSystemFinalProject.Api.Data;

namespace VillarKlarence_WebSystemFinalProject.Tests.Infrastructure;

/// <summary>
/// Hosts the real API pipeline in memory (same Program.cs, controllers, JWT auth, validation),
/// with SQL Server replaced by the EF Core InMemory provider.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _databaseName = "api-tests-" + Guid.NewGuid();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=unused-in-tests");
        builder.UseSetting("Jwt:Key", "integration-test-signing-key-0123456789-abcdefghijklmnop");
        builder.UseSetting("Seed:DemoPassword", TestData.DemoPassword);
        builder.UseSetting("Database:SkipInitialization", "true");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(_databaseName));
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        TestData.SeedAcademicData(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        // Same initializer the API runs at startup (InMemory: no migrations, only Identity seeding).
        await DatabaseInitializer.InitializeAsync(Services);
    }

    public new async Task DisposeAsync() => await base.DisposeAsync();
}
