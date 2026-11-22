using System.Globalization;

namespace JPCenter.Modules.Academic.Application.Common;

/// <summary>Giờ học dạng "HH:mm" giống frontend; đổi giờ địa phương VN (UTC+7, không đổi giờ mùa hè) ↔ mốc thời gian</summary>
public static class TimeText
{
    private static readonly TimeSpan Vietnam = TimeSpan.FromHours(7);

    /// <summary>Ngày + giờ địa phương (giờ học) → mốc thời gian UTC (PostgreSQL chỉ nhận UTC)</summary>
    public static DateTimeOffset At(DateOnly date, TimeOnly time) => new DateTimeOffset(date.ToDateTime(time), Vietnam).ToUniversalTime();

    /// <summary>Mốc thời gian → "HH:mm" giờ Việt Nam</summary>
    public static string Of(DateTimeOffset at) => Of(TimeOnly.FromDateTime(at.ToOffset(Vietnam).DateTime));

    public static string Of(TimeOnly t) => t.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static string? Of(TimeOnly? t) => t is { } v ? Of(v) : null;

    /// <summary>Mốc thời gian → "HH:mm dd/MM" giờ Việt Nam (nội dung thông báo)</summary>
    public static string Moment(DateTimeOffset at) => at.ToOffset(Vietnam).ToString("HH:mm dd/MM", CultureInfo.InvariantCulture);

    /// <summary>Ngày (giờ Việt Nam) của mốc thời gian</summary>
    public static DateOnly DateOf(DateTimeOffset at) => DateOnly.FromDateTime(at.ToOffset(Vietnam).DateTime);
}
