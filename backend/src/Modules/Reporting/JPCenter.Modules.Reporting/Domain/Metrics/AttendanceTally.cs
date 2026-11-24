namespace JPCenter.Modules.Reporting.Domain.Metrics;

/// <summary>Đếm lượt điểm danh: <paramref name="Attended"/> = có mặt + đi muộn, <paramref name="Total"/> = mọi lượt đã điểm danh (BC-05 bước 3).</summary>
public readonly record struct AttendanceTally(int Attended, int Total)
{
    public static readonly AttendanceTally Empty = new(0, 0);

    /// <summary>% chuyên cần; chưa có lượt nào → null (BC-05.E1)</summary>
    public int? Rate => Ratio.Percent(Attended, Total);

    public static AttendanceTally operator +(AttendanceTally a, AttendanceTally b) => new(a.Attended + b.Attended, a.Total + b.Total);
}
