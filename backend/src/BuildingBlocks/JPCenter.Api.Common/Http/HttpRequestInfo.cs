using JPCenter.Application.Common.Security;
using Microsoft.AspNetCore.Http;

namespace JPCenter.Api.Common.Http;

/// <summary>IP (đã qua ForwardedHeaders), thiết bị rút gọn từ User-Agent, mã truy vết của request hiện tại.</summary>
public sealed class HttpRequestInfo(IHttpContextAccessor accessor) : IRequestInfo
{
    public string Ip => accessor.HttpContext?.Connection.RemoteIpAddress?.ToString() ?? "unknown";

    public string Device => DescribeDevice(accessor.HttpContext?.Request.Headers.UserAgent.ToString());

    public string? TraceId => accessor.HttpContext?.TraceIdentifier;

    /// <summary>"Mozilla/5.0 (Windows NT 10.0...) Chrome/131" → "Chrome · Windows"</summary>
    public static string DescribeDevice(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Không rõ";
        var ua = userAgent;
        var browser =
            ua.Contains("Edg/", StringComparison.Ordinal) ? "Edge"
            : ua.Contains("OPR/", StringComparison.Ordinal) ? "Opera"
            : ua.Contains("Firefox/", StringComparison.Ordinal) ? "Firefox"
            : ua.Contains("Zalo", StringComparison.OrdinalIgnoreCase) ? "Zalo"
            : ua.Contains("Chrome/", StringComparison.Ordinal) ? "Chrome"
            : ua.Contains("Safari/", StringComparison.Ordinal) ? "Safari"
            : ua.Length > 40 ? ua[..40] : ua;
        var os =
            ua.Contains("iPhone", StringComparison.Ordinal) || ua.Contains("iPad", StringComparison.Ordinal) ? "iOS"
            : ua.Contains("Android", StringComparison.Ordinal) ? "Android"
            : ua.Contains("Windows", StringComparison.Ordinal) ? "Windows"
            : ua.Contains("Mac OS", StringComparison.Ordinal) ? "macOS"
            : ua.Contains("Linux", StringComparison.Ordinal) ? "Linux"
            : null;
        return os is null ? browser : $"{browser} · {os}";
    }
}
