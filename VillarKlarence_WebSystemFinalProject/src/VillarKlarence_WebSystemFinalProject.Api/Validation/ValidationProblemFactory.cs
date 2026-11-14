using Microsoft.AspNetCore.Mvc;

namespace VillarKlarence_WebSystemFinalProject.Api.Validation;

/// <summary>
/// Replaces the default ApiBehaviorOptions.InvalidModelStateResponseFactory so that every 400 has:
///  - camelCase field keys (grade, studentId, ...) that match the JSON the client sent;
///  - a friendly message instead of raw JSON-converter text. In particular, when "grade" cannot be
///    converted to an int (87.5, "abc", true...) the message is "Grade must be a whole number from 0 to 100.";
///  - a "detail" summary so the error is readable in Postman without expanding "errors".
/// </summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var modelState = context.ModelState;

        // Keys that start with "$" (or are empty) come from the JSON input formatter, i.e. the body could not be read.
        var hasBodyErrors = modelState.Any(kv => kv.Value is { Errors.Count: > 0 } && (kv.Key.Length == 0 || kv.Key.StartsWith('$')));
        var parameterNames = context.ActionDescriptor.Parameters
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (key, entry) in modelState.Where(kv => kv.Value is not null).Select(kv => (kv.Key, kv.Value!)))
        {
            if (entry.Errors.Count == 0) continue;

            if (key.Length == 0 || key == "$")
            {
                Add(errors, "body", "The request body is missing or is not valid JSON.");
                continue;
            }

            if (key.StartsWith('$'))
            {
                var field = JsonPathToField(key);
                Add(errors, field, field.Equals("grade", StringComparison.OrdinalIgnoreCase)
                    ? GradeRules.GradeMessage
                    : $"The value supplied for '{field}' is not in the correct format.");
                continue;
            }

            // "The request field is required." is noise when the body itself failed to deserialize.
            if (hasBodyErrors && parameterNames.Contains(key)) continue;

            var name = ToCamelCase(key);
            foreach (var error in entry.Errors)
                Add(errors, name, string.IsNullOrWhiteSpace(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage);
        }

        var problem = new ValidationProblemDetails(errors.ToDictionary(kv => kv.Key, kv => kv.Value.ToArray()))
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "One or more validation errors occurred.",
            Detail = string.Join(" ", errors.Values.SelectMany(v => v).Distinct()),
            Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            Instance = context.HttpContext.Request.Path
        };
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" }
        };
    }

    private static void Add(Dictionary<string, List<string>> errors, string field, string message)
    {
        if (!errors.TryGetValue(field, out var list)) errors[field] = list = [];
        if (!list.Contains(message)) list.Add(message);
    }

    /// <summary>"$.grade" -> "grade", "$.items[0].name" -> "items".</summary>
    private static string JsonPathToField(string path)
    {
        var trimmed = path.TrimStart('$').TrimStart('.');
        var end = trimmed.IndexOfAny(['.', '[']);
        var first = end >= 0 ? trimmed[..end] : trimmed;
        return first.Length == 0 ? "body" : ToCamelCase(first);
    }

    private static string ToCamelCase(string name)
    {
        var last = name.Contains('.') ? name[(name.LastIndexOf('.') + 1)..] : name;
        return last.Length == 0 ? "body" : char.ToLowerInvariant(last[0]) + last[1..];
    }
}
