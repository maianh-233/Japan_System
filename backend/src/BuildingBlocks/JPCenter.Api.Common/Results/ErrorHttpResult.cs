using JPCenter.Api.Common.Errors;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace JPCenter.Api.Common.Results;

/// <summary>
/// Lỗi nghiệp vụ → ProblemDetails. Dựng lúc ghi response để <c>traceId</c> khớp header X-Request-Id.
/// Có <see cref="Value"/> / <see cref="StatusCode"/> để filter (Idempotency, Audit) đọc được kết quả.
/// </summary>
public sealed class ErrorHttpResult(Error error) : IResult, IStatusCodeHttpResult, IValueHttpResult, IValueHttpResult<ProblemDetails>,
    IContentTypeHttpResult
{
    public Error Error { get; } = error;

    public int? StatusCode => ProblemFactory.StatusOf(Error.Type);

    public ProblemDetails Value => ProblemFactory.From(Error);

    object IValueHttpResult.Value => Value;

    public string ContentType => "application/problem+json";

    public Task ExecuteAsync(HttpContext httpContext)
    {
        var problem = ProblemFactory.From(Error, httpContext);
        httpContext.Response.StatusCode = StatusCode!.Value;
        return httpContext.Response.WriteAsJsonAsync(problem, (System.Text.Json.JsonSerializerOptions?)null, ContentType, httpContext.RequestAborted);
    }
}
