using System.Globalization;
using JPCenter.Api.Common.Errors;
using Microsoft.AspNetCore.Http;

namespace JPCenter.Api.Common.RateLimiting;

/// <summary>Người đang bị tạm khóa (spam nhiều lần) → mọi thao tác ghi trả 429 ngay; vẫn được xem (GET).</summary>
public sealed class PenaltyMiddleware(RequestDelegate next, PenaltyBox penalty)
{
    public Task InvokeAsync(HttpContext http)
    {
        if (!RateLimitingExtensions.IsWrite(http) || !penalty.IsBlocked(RateLimitingExtensions.PartitionKey(http), out var remaining))
            return next(http);

        var seconds = (int)Math.Ceiling(remaining.TotalSeconds);
        var minutes = (int)Math.Ceiling(remaining.TotalMinutes);
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        return ProblemFactory.WriteAsync(http, StatusCodes.Status429TooManyRequests, "system.temporarily_blocked",
            $"Bạn đã gửi quá nhiều yêu cầu liên tục. Thao tác thêm / sửa / xóa bị tạm khóa {minutes} phút.",
            new Dictionary<string, object?> { ["retryAfter"] = seconds });
    }
}
