using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace JPCenter.Api.Common.Middleware;

/// <summary>
/// M2 — Mỗi request 1 mã truy vết: nhận <c>X-Request-Id</c> từ client (nếu hợp lệ) hoặc tự tạo; trả lại trong header,
/// có trong log, lỗi (<c>traceId</c>) và nhật ký. Người dùng báo lỗi kèm mã → tra ra ngay.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Request-Id";

    [GeneratedRegex("^[A-Za-z0-9._-]{8,64}$")]
    private static partial Regex ValidId();

    public async Task InvokeAsync(HttpContext http)
    {
        var incoming = http.Request.Headers[Header].ToString();
        var id = ValidId().IsMatch(incoming) ? incoming : Activity.Current?.TraceId.ToString() ?? Guid.NewGuid().ToString("N");

        http.TraceIdentifier = id;
        http.Response.OnStarting(() =>
        {
            http.Response.Headers[Header] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { ["RequestId"] = id }))
            await next(http);
    }
}
