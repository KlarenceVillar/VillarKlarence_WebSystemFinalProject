using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using VillarKlarence_WebSystemFinalProject.Api.Data;
using VillarKlarence_WebSystemFinalProject.Api.Middleware;
using VillarKlarence_WebSystemFinalProject.Api.Models;
using VillarKlarence_WebSystemFinalProject.Api.Security;
using VillarKlarence_WebSystemFinalProject.Api.Services;
using VillarKlarence_WebSystemFinalProject.Api.Validation;

const string WebClientCorsPolicy = "WebClient";

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- configuration
// Jwt:Key is loaded from dotnet user-secrets (setup-secrets.cmd). ValidateOnStart stops the API
// with a clear message if it is missing.
builder.Services.AddOptions<JwtSettings>()
    .Bind(builder.Configuration.GetSection(JwtSettings.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.TryAddSingleton(TimeProvider.System);

// ---------------------------------------------------------------- database (one DB: StudentGradeManagementDB)
builder.Services.AddDbContext<AppDbContext>((services, options) =>
{
    var connectionString = services.GetRequiredService<IConfiguration>().GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ConnectionStrings:DefaultConnection is not configured.");
    options.UseSqlServer(connectionString);
});

// ---------------------------------------------------------------- ASP.NET Core Identity (hashed passwords, roles)
builder.Services
    .AddIdentityCore<ApplicationUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AppDbContext>();

// ---------------------------------------------------------------- JWT bearer authentication
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtSettings>>((options, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        options.MapInboundClaims = false; // keep claim types exactly as issued by TokenService
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = ClaimTypes.Name,
            RoleClaimType = ClaimTypes.Role
        };
        options.Events = JwtProblemEvents.Create(); // 401/403 as JSON ProblemDetails
    });

// Every endpoint requires a signed-in user unless it explicitly says [AllowAnonymous].
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// ---------------------------------------------------------------- CORS for the MVC site
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                     ?? ["https://localhost:7102"];
builder.Services.AddCors(options => options.AddPolicy(WebClientCorsPolicy, policy => policy
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location")));

// ---------------------------------------------------------------- MVC controllers, JSON and validation
builder.Services.AddProblemDetails(options =>
    options.CustomizeProblemDetails = ctx =>
        ctx.ProblemDetails.Extensions.TryAdd("traceId",
            System.Diagnostics.Activity.Current?.Id ?? ctx.HttpContext.TraceIdentifier));

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Numbers must be real JSON numbers: "grade": "90" or "abc" is rejected, not silently converted.
        options.JsonSerializerOptions.NumberHandling = JsonNumberHandling.Strict;
    })
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ValidationProblemFactory.Create);

// ---------------------------------------------------------------- application services
builder.Services.AddSingleton<IRemarksCalculator, RemarksCalculator>();
builder.Services.AddScoped<IGradeService, GradeService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IAccountService, AccountService>();

var app = builder.Build();

// ---------------------------------------------------------------- pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>(); // unexpected errors -> 500 ProblemDetails, no stack traces
app.UseStatusCodePages();                          // empty 404/405 responses -> ProblemDetails
app.UseHttpsRedirection();
app.UseCors(WebClientCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapGet("/", () => Results.Ok(new
{
    name = "UST Angelicum College - Student Grade Management API",
    status = "running",
    login = "POST /api/auth/login",
    documentation = "See docs/API_ENDPOINTS.md"
})).AllowAnonymous().ExcludeFromDescription();

// ---------------------------------------------------------------- database: verify script, migrate, seed
if (!app.Configuration.GetValue("Database:SkipInitialization", false))
{
    try
    {
        await DatabaseInitializer.InitializeAsync(app.Services);
    }
    catch (Exception ex)
    {
        app.Logger.LogCritical(ex, "Database initialization failed: {Message}", ex.Message);
        Environment.ExitCode = 1;
        return;
    }
}

app.Run();

/// <summary>Exposed for WebApplicationFactory integration tests.</summary>
public partial class Program;
