using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Api.Middleware;

/// <summary>
/// Global safety net: any unhandled exception becomes a 500 ProblemDetails JSON response.
/// Details are logged on the server; the client never sees a stack trace.
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, IProblemDetailsService problemDetailsService)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client disconnected; nothing to send back.
            logger.LogDebug("Request {Path} was cancelled by the client.", context.Request.Path);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path} (trace {TraceId}).",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);

            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;

            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context,
                ProblemDetails = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected error occurred.",
                    Detail = "The server could not complete your request. Please try again later.",
                    Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
                }
            });
        }
    }
}
