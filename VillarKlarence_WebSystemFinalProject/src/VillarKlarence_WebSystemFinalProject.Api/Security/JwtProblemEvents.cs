using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace VillarKlarence_WebSystemFinalProject.Api.Security;

/// <summary>Makes 401 and 403 responses JSON ProblemDetails (clear in Postman and easy for fetch() to read).</summary>
public static class JwtProblemEvents
{
    public static JwtBearerEvents Create() => new()
    {
        OnChallenge = async context =>
        {
            // Suppress the default empty 401 and write our own body.
            context.HandleResponse();
            if (context.Response.HasStarted) return;

            var detail = context.AuthenticateFailure switch
            {
                SecurityTokenExpiredException => "Your session has expired. Please sign in again.",
                not null => "The access token is invalid. Please sign in again.",
                null when string.IsNullOrEmpty(context.Request.Headers.Authorization) =>
                    "Authentication is required. Sign in and send the header 'Authorization: Bearer <token>'.",
                _ => "Authentication is required. Please sign in."
            };

            context.Response.Headers.WWWAuthenticate = "Bearer";
            await WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized, "Unauthorized", detail);
        },
        OnForbidden = context => WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden, "Forbidden",
            "You do not have permission to perform this action.")
    };

    private static async Task WriteAsync(HttpContext http, int status, string title, string detail)
    {
        http.Response.StatusCode = status;
        var writer = http.RequestServices.GetRequiredService<IProblemDetailsService>();
        await writer.WriteAsync(new ProblemDetailsContext
        {
            HttpContext = http,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = title,
                Detail = detail,
                Type = status == StatusCodes.Status401Unauthorized
                    ? "https://tools.ietf.org/html/rfc9110#section-15.5.2"
                    : "https://tools.ietf.org/html/rfc9110#section-15.5.4",
                Instance = http.Request.Path
            }
        });
    }
}
