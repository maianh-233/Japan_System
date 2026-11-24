namespace JPCenter.Modules.Reporting.Domain.Metrics;

/// <summary>Công suất phòng trong tuần (BC-05 bước 5): giờ có buổi học / giờ mở cửa lớp học.</summary>
public static class RoomUtilization
{
    /// <summary>Giờ mở cửa lớp học mỗi tuần: T2 → T7, 8:00 → 21:30 = 6 × 13,5 giờ</summary>
    public const decimal OpenHoursPerWeek = 6 * 13.5m;

    /// <summary>Số giờ (1 chữ số thập phân) từ tổng phút có lớp</summary>
    public static decimal Hours(int minutes) => decimal.Round(minutes / 60m, 1, MidpointRounding.AwayFromZero);

    /// <summary>% công suất, làm tròn số nguyên — có thể &gt; 100 khi xếp lớp ngoài giờ mở cửa</summary>
    public static int Rate(int minutes) => Ratio.Percent(minutes / 60m, OpenHoursPerWeek) ?? 0;

    /// <summary>Tuần báo cáo chứa <paramref name="today"/>: thứ 2 → chủ nhật</summary>
    public static (DateOnly From, DateOnly To) WeekOf(DateOnly today)
    {
        var back = ((int)today.DayOfWeek + 6) % 7;
        var from = today.AddDays(-back);
        return (from, from.AddDays(6));
    }
}
