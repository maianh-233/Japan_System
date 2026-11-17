using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using JPCenter.Api.Common.Errors;
using JPCenter.Api.Common.RateLimiting;
using JPCenter.Application.Common.Idempotency;
using JPCenter.SharedKernel.Primitives;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using HttpResults = Microsoft.AspNetCore.Http.Results;

namespace JPCenter.Api.Common.Idempotency;

/// <summary>Endpoint bắt buộc gửi <c>Idempotency-Key</c> — metadata cho test / tài liệu.</summary>
public sealed record IdempotentEndpoint;

/// <summary>
/// F1 — Chống gửi trùng (request-pipeline.md mục 5): cùng <c>Idempotency-Key</c> → không chạy lại handler, trả đúng response cũ.
/// Bắt buộc cho thao tác tiền / ghi danh / gửi tin / nhập file.
/// </summary>
public static partial class IdempotencyExtensions
{
    public const string Header = "Idempotency-Key";
    public const string ReplayedHeader = "Idempotent-Replayed";

    [GeneratedRegex("^[A-Za-z0-9_-]{8,100}$")]
    private static partial Regex ValidKey();

    public static TBuilder Idempotent<TBuilder>(this TBuilder builder) where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(new IdempotentEndpoint());
        builder.AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var key = http.Request.Headers[Header].ToString();
            if (!ValidKey().IsMatch(key))
                return new Results.ErrorHttpResult(Error.Validation("system.idempotency_key_required",
                    "Thiếu hoặc sai mã chống gửi trùng (Idempotency-Key). Vui lòng tải lại trang rồi thử lại."));

            var store = http.RequestServices.GetRequiredService<IIdempotencyStore>();
            var json = http.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;
            var scope = RateLimitingExtensions.PartitionKey(http);
            var hash = Hash(http, ctx.Arguments, json);

            var begin = await store.TryBeginAsync(scope, key, hash, http.RequestAborted);
            switch (begin.State)
            {
                case IdempotencyState.InProgress:
                    return new Results.ErrorHttpResult(Error.Conflict("system.request_in_progress",
                        "Yêu cầu đang được xử lý, vui lòng chờ — không bấm lại."));
                case IdempotencyState.Mismatch:
                    return HttpResults.Problem(ProblemFactory.Create(StatusCodes.Status422UnprocessableEntity, "system.idempotency_mismatch",
                        "Mã chống gửi trùng đã dùng cho nội dung khác. Vui lòng tải lại trang.", http: http));
                case IdempotencyState.Completed:
                    http.Response.Headers[ReplayedHeader] = "true";
                    var r = begin.Response!;
                    return r.Body is null
                        ? HttpResults.StatusCode(r.StatusCode)
                        : HttpResults.Text(r.Body, r.ContentType ?? "application/json", Encoding.UTF8, r.StatusCode);
            }

            object? result;
            try
            {
                result = await next(ctx);
            }
            catch
            {
                await store.ReleaseAsync(scope, key, CancellationToken.None);
                throw;
            }

            var status = (result as IStatusCodeHttpResult)?.StatusCode ?? StatusCodes.Status200OK;
            if (status >= 500)
            {
                await store.ReleaseAsync(scope, key, CancellationToken.None);
                return result;
            }

            var body = result is IValueHttpResult { Value: { } value } ? JsonSerializer.Serialize(value, value.GetType(), json) : null;
            var contentType = (result as IContentTypeHttpResult)?.ContentType ?? (body is null ? null : "application/json");
            await store.CompleteAsync(scope, key, new StoredResponse(status, body, contentType), CancellationToken.None);
            return result;
        });
        return builder;
    }

    /// <summary>Băm nội dung request (đường dẫn + tham số dữ liệu đã bind) — cùng key mà khác nội dung → 422.</summary>
    private static string Hash(HttpContext http, IList<object?> arguments, JsonSerializerOptions json)
    {
        var sb = new StringBuilder(http.Request.Method).Append(' ').Append(http.Request.Path);
        foreach (var arg in arguments)
        {
            if (arg is null || !IsData(arg.GetType())) continue;
            sb.Append('|').Append(JsonSerializer.Serialize(arg, arg.GetType(), json));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString())));
    }

    private static bool IsData(Type t) =>
        t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(Guid) || t == typeof(decimal) || t == typeof(DateOnly)
        || t == typeof(DateTimeOffset) || t.Namespace?.StartsWith("JPCenter.", StringComparison.Ordinal) == true && !t.IsInterface
            && !t.Name.EndsWith("Handler", StringComparison.Ordinal);
}
