using System.Diagnostics;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JPCenter.Api.Common.Errors;

/// <summary>
/// Dựng ProblemDetails thống nhất: <c>{ code, title, status, errors?, traceId }</c> (CLAUDE.md mục 8).
/// <c>title</c> là thông điệp tiếng Việt cho người dùng.
/// </summary>
public static class ProblemFactory
{
    public static int StatusOf(ErrorType type) => type switch
    {
        ErrorType.Validation => StatusCodes.Status400BadRequest,
        ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
        ErrorType.Forbidden => StatusCodes.Status403Forbidden,
        ErrorType.NotFound => StatusCodes.Status404NotFound,
        ErrorType.Conflict => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status500InternalServerError,
    };

    public static ProblemDetails From(Error error, HttpContext? http = null) =>
        Create(StatusOf(error.Type), error.Code, error.Message, error.FieldErrors, http);

    public static ProblemDetails Create(int status, string code, string title,
        IReadOnlyDictionary<string, string[]>? fieldErrors = null, HttpContext? http = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Type = $"https://httpstatuses.io/{status}" };
        problem.Extensions["code"] = code;
        if (fieldErrors is { Count: > 0 }) problem.Extensions["errors"] = fieldErrors;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? http?.TraceIdentifier;
        return problem;
    }
}
