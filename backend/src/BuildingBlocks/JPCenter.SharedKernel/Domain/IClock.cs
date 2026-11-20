namespace JPCenter.SharedKernel.Domain;

/// <summary>Giờ hiện tại — KHÔNG dùng DateTime.Now.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Ngày hôm nay theo giờ Việt Nam.</summary>
    DateOnly Today { get; }

    /// <summary>Ngày giờ hiện tại theo giờ Việt Nam (không múi giờ) — so với giờ học / giờ hẹn lưu dạng giờ địa phương.</summary>
    DateTime LocalNow { get; }
}
