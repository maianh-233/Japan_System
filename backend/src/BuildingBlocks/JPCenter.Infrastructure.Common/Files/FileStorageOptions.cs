using Microsoft.Extensions.Configuration;

namespace JPCenter.Infrastructure.Common.Files;

/// <summary>
/// Cấu hình kho file S3-compatible, đọc từ <c>Storage:*</c> (.env: <c>Storage__ServiceUrl</c>...). ADR 0003.
/// <list type="bullet">
/// <item>Local: SeaweedFS trong docker — <c>http://localhost:8333</c>, khóa tùy ý, <c>CreateBucket=true</c>.</item>
/// <item>Thật: Cloudflare R2 — <c>https://&lt;account-id&gt;.r2.cloudflarestorage.com</c>, <c>Region=auto</c>, khóa API R2.</item>
/// </list>
/// Chưa cấu hình <c>ServiceUrl</c> → mọi thao tác file báo lỗi hệ thống (không âm thầm bỏ qua như email).
/// </summary>
public sealed record FileStorageOptions(
    string? ServiceUrl,
    string Region,
    string Bucket,
    string? AccessKey,
    string? SecretKey,
    /// <summary>Tự tạo bucket nếu chưa có — chỉ bật ở local</summary>
    bool CreateBucket,
    /// <summary>Thời hạn mặc định của link tải (phút)</summary>
    int LinkMinutes)
{
    public bool IsConfigured => !string.IsNullOrWhiteSpace(ServiceUrl);

    public bool UsesHttps => ServiceUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true;

    public static FileStorageOptions Load(IConfiguration c) => new(
        c["Storage:ServiceUrl"],
        c["Storage:Region"] ?? "auto",
        c["Storage:Bucket"] ?? "jpcenter",
        c["Storage:AccessKey"],
        c["Storage:SecretKey"],
        c.GetValue("Storage:CreateBucket", false),
        Math.Clamp(c.GetValue("Storage:LinkMinutes", 15), 1, 7 * 24 * 60));
}
