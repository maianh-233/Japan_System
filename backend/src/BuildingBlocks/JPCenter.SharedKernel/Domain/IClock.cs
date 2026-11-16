namespace JPCenter.SharedKernel.Domain;

/// <summary>Giờ hiện tại — KHÔNG dùng DateTime.Now.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Ngày hôm nay theo giờ Việt Nam.</summary>
    DateOnly Today { get; }
}
