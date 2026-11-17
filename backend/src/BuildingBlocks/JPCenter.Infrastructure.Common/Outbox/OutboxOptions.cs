using Microsoft.Extensions.Configuration;

namespace JPCenter.Infrastructure.Common.Outbox;

/// <summary>Cấu hình Outbox (<c>Outbox:*</c>). Mặc định hợp cho 1 instance.</summary>
/// <param name="Enabled">Tắt = không chạy tiến trình gửi (sự kiện vẫn được ghi)</param>
/// <param name="PollInterval">Kiểm tra định kỳ dự phòng (bình thường được đánh thức ngay sau khi lưu)</param>
/// <param name="RetryBase">Lần thử thứ n chờ RetryBase × 2^(n-1), tối đa 1 giờ</param>
/// <param name="KeepProcessed">Sự kiện đã gửi giữ lại bao lâu để tra cứu rồi xóa</param>
public sealed record OutboxOptions(bool Enabled, TimeSpan PollInterval, int BatchSize, int MaxAttempts, TimeSpan RetryBase, TimeSpan KeepProcessed)
{
    public static OutboxOptions Load(IConfiguration c) => new(
        c.GetValue("Outbox:Enabled", true),
        TimeSpan.FromMilliseconds(c.GetValue("Outbox:PollMilliseconds", 5000)),
        c.GetValue("Outbox:BatchSize", 20),
        c.GetValue("Outbox:MaxAttempts", 8),
        TimeSpan.FromMilliseconds(c.GetValue("Outbox:RetryBaseMilliseconds", 10_000)),
        TimeSpan.FromDays(c.GetValue("Outbox:KeepProcessedDays", 7)));

    public TimeSpan Backoff(int attempts)
    {
        var ms = RetryBase.TotalMilliseconds * Math.Pow(2, Math.Max(0, attempts - 1));
        return TimeSpan.FromMilliseconds(Math.Min(ms, TimeSpan.FromHours(1).TotalMilliseconds));
    }
}
