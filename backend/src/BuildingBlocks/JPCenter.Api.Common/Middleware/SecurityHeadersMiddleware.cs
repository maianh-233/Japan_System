using Microsoft.AspNetCore.Http;

namespace JPCenter.Api.Common.Middleware;

/// <summary>
/// M4 — Header bảo mật cho mọi response của API (chỉ trả JSON, không có trang HTML).
/// Dữ liệu cá nhân không được cache ở trình duyệt / proxy: mặc định <c>Cache-Control: no-store</c>.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext http)
    {
        http.Response.OnStarting(() =>
        {
            var h = http.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "no-referrer";
            h["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            if (!h.ContainsKey("Cache-Control")) h.CacheControl = "no-store";
            return Task.CompletedTask;
        });
        return next(http);
    }
}
