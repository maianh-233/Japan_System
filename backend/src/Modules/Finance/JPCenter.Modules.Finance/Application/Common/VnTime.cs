namespace JPCenter.Modules.Finance.Application.Common;

/// <summary>Đầu ngày theo giờ Việt Nam (UTC+7) đổi ra UTC — PostgreSQL <c>timestamptz</c> chỉ nhận offset 0</summary>
public static class VnTime
{
    private static readonly TimeSpan Offset = TimeSpan.FromHours(7);

    public static DateTimeOffset StartOf(DateOnly day) => new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), Offset).ToUniversalTime();
}
