namespace VillarKlarence_WebSystemFinalProject.Api.Services;

public enum ServiceStatus
{
    Success,
    NotFound,
    Conflict,
    ValidationFailed,
    Forbidden
}

/// <summary>Outcome of a grade write operation; controllers translate it into HTTP status codes.</summary>
public sealed class ServiceResult<T>
{
    public ServiceStatus Status { get; private init; }
    public T? Value { get; private init; }
    public string? Message { get; private init; }

    /// <summary>Field errors keyed by camelCase JSON property name (for ValidationProblemDetails).</summary>
    public IDictionary<string, string[]> Errors { get; private init; } = new Dictionary<string, string[]>();

    public bool Succeeded => Status == ServiceStatus.Success;

    public static ServiceResult<T> Success(T value) => new() { Status = ServiceStatus.Success, Value = value };

    public static ServiceResult<T> NotFound(string message) => new() { Status = ServiceStatus.NotFound, Message = message };

    public static ServiceResult<T> Conflict(string message) => new() { Status = ServiceStatus.Conflict, Message = message };

    public static ServiceResult<T> Forbidden(string message) => new() { Status = ServiceStatus.Forbidden, Message = message };

    public static ServiceResult<T> Invalid(string field, string message) => new()
    {
        Status = ServiceStatus.ValidationFailed,
        Message = message,
        Errors = new Dictionary<string, string[]> { [field] = [message] }
    };
}
