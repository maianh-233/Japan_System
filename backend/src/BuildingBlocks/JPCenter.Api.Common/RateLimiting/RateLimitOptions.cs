namespace JPCenter.Api.Common.RateLimiting;

/// <summary>Ngưỡng rate limit ⚙️ — mục <c>RateLimit</c> trong cấu hình (vd <c>RateLimit__WriteBurst=10</c> trong .env).</summary>
public sealed class RateLimitOptions
{
    public const string Section = "RateLimit";

    /// <summary>Tắt hẳn (chỉ dùng khi test hiệu năng)</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Mọi request / người / phút (lưới an toàn)</summary>
    public int GlobalPerMinute { get; set; } = 300;

    /// <summary>Ghi thường: gửi liền tối đa N lần / endpoint...</summary>
    public int WriteBurst { get; set; } = 10;

    /// <summary>...sau đó nạp lại N lần mỗi giây</summary>
    public int WriteRefillPerSecond { get; set; } = 1;

    /// <summary>Ghi nhạy cảm: N lần / 10 giây / endpoint</summary>
    public int SensitivePer10Seconds { get; set; } = 3;

    /// <summary>Endpoint ẩn danh: N lần / phút / IP (nhiều người chung wifi trung tâm → đừng đặt quá thấp)</summary>
    public int AnonymousPerMinute { get; set; } = 20;

    /// <summary>Vi phạm N lần trong 10 phút → khóa thao tác ghi</summary>
    public int PenaltyViolations { get; set; } = 3;

    /// <summary>Thời gian khóa thao tác ghi (phút)</summary>
    public int PenaltyMinutes { get; set; } = 5;
}
