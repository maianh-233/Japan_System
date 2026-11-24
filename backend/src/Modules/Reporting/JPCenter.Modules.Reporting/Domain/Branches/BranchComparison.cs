using JPCenter.Modules.Reporting.Domain.Metrics;

namespace JPCenter.Modules.Reporting.Domain.Branches;

/// <summary>1 dòng so sánh chi nhánh (BC-06) — số thô + tỷ lệ đã tính + cờ cần chú ý.</summary>
public sealed record BranchComparison(BranchFigures Figures, int? Conversion, int? Fill, int? Attendance)
{
    /// <summary>BC-06.E1 — chuyên cần dưới ngưỡng này (%) thì đánh dấu</summary>
    public const int LowAttendancePercent = 85;

    /// <summary>Chuyên cần thấp (chưa có lượt điểm danh nào → không đánh dấu)</summary>
    public bool LowAttendance => Attendance < LowAttendancePercent;

    /// <summary>Còn nợ quá hạn</summary>
    public bool HasOverdue => Figures.Overdue > 0;

    /// <summary>BC-06.E1 — cần chú ý</summary>
    public bool NeedsAttention => LowAttendance || HasOverdue;

    public static BranchComparison From(BranchFigures f) =>
        new(f, Ratio.Percent(f.WonLeads, f.Leads), Ratio.Percent(f.FilledSeats, f.Seats), f.Attendance.Rate);

    /// <summary>Các chi nhánh theo thứ tự truyền vào (thứ tự danh mục chi nhánh)</summary>
    public static IReadOnlyList<BranchComparison> Compare(IEnumerable<BranchFigures> branches) => branches.Select(From).ToList();
}
