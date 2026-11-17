namespace JPCenter.Api.Common.RateLimiting;

/// <summary>
/// Tên policy rate limit (request-pipeline.md mục 4) — gắn bằng <c>.RequireRateLimiting(RateLimitPolicies.X)</c>.
/// Mọi request đã có sẵn giới hạn chung + giới hạn ghi (POST/PUT/PATCH/DELETE) theo người dùng + endpoint.
/// </summary>
public static class RateLimitPolicies
{
    /// <summary>Endpoint không cần đăng nhập (đăng nhập, quên mật khẩu) — theo IP</summary>
    public const string Anonymous = "anon";

    /// <summary>Tiền / ghi danh / gửi tin hàng loạt — chặt hơn ghi thường</summary>
    public const string WriteSensitive = "write-sensitive";

    /// <summary>Xuất file — mỗi người 1 lần chạy cùng lúc</summary>
    public const string Export = "export";

    /// <summary>Nhập file Excel — mỗi người 1 lần chạy cùng lúc</summary>
    public const string Import = "import";
}
