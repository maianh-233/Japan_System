using JPCenter.SharedKernel.Domain;

namespace JPCenter.Infrastructure.Common.Time;

/// <summary>Giờ thật của hệ thống. "Hôm nay" tính theo giờ Việt Nam (Asia/Ho_Chi_Minh).</summary>
public sealed class SystemClock(TimeProvider time) : IClock
{
    private static readonly TimeZoneInfo Vietnam = TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh");

    public DateTimeOffset UtcNow => time.GetUtcNow();

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime LocalNow => TimeZoneInfo.ConvertTime(UtcNow, Vietnam).DateTime;
}
