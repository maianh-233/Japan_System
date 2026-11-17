using JPCenter.SharedKernel.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace JPCenter.Api.Common.RateLimiting;

/// <summary>
/// Phạt tăng dần người cố tình spam (request-pipeline.md mục 4): vượt ngưỡng nhiều lần trong 10 phút → khóa thao tác ghi.
/// Lưu trong bộ nhớ — khởi động lại app thì hết khóa (chấp nhận được, ADR 0002).
/// </summary>
public sealed class PenaltyBox(IMemoryCache cache, IClock clock, IOptions<RateLimitOptions> options)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(10);
    private readonly Lock _lock = new();

    /// <summary>Ghi 1 lần vượt ngưỡng. Trả true nếu lần này làm người đó BỊ KHÓA (để ghi nhật ký 1 lần).</summary>
    public bool RecordViolation(string partitionKey)
    {
        var o = options.Value;
        lock (_lock)
        {
            var key = $"rl-violations:{partitionKey}";
            var count = (cache.TryGetValue(key, out int c) ? c : 0) + 1;
            cache.Set(key, count, Window);
            if (count < o.PenaltyViolations || IsBlocked(partitionKey, out _)) return false;

            cache.Set($"rl-blocked:{partitionKey}", clock.UtcNow.AddMinutes(o.PenaltyMinutes), TimeSpan.FromMinutes(o.PenaltyMinutes));
            cache.Remove(key);
            return true;
        }
    }

    public bool IsBlocked(string partitionKey, out TimeSpan remaining)
    {
        remaining = TimeSpan.Zero;
        if (!cache.TryGetValue($"rl-blocked:{partitionKey}", out DateTimeOffset until)) return false;
        remaining = until - clock.UtcNow;
        return remaining > TimeSpan.Zero;
    }
}
