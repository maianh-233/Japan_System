using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Reporting.Domain.Periods;

/// <summary>
/// Kỳ báo cáo N tháng (3 / 6 / 12) tính theo giờ Việt Nam: tháng hiện tại + N − 1 tháng trước, từ ngày 1 (user story 17, quy tắc vàng 4).
/// </summary>
public sealed record ReportPeriod
{
    public static readonly IReadOnlyList<int> AllowedMonths = [3, 6, 12];

    private ReportPeriod(int months, DateOnly today)
    {
        Months = months;
        Today = today;
        CurrentMonth = new DateOnly(today.Year, today.Month, 1);
        From = CurrentMonth.AddMonths(-(months - 1));
        MonthStarts = Enumerable.Range(0, months).Select(i => From.AddMonths(i)).ToList();
    }

    public int Months { get; }

    /// <summary>Hôm nay (giờ VN)</summary>
    public DateOnly Today { get; }

    /// <summary>Ngày 1 của tháng đầu kỳ</summary>
    public DateOnly From { get; }

    /// <summary>Ngày cuối kỳ = hôm nay</summary>
    public DateOnly To => Today;

    /// <summary>Ngày 1 của tháng hiện tại</summary>
    public DateOnly CurrentMonth { get; }

    /// <summary>Ngày 1 của tháng trước</summary>
    public DateOnly PreviousMonth => CurrentMonth.AddMonths(-1);

    /// <summary>Ngày 1 của từng tháng trong kỳ, cũ → mới</summary>
    public IReadOnlyList<DateOnly> MonthStarts { get; }

    public static Result<ReportPeriod> Create(int months, DateOnly today) =>
        AllowedMonths.Contains(months) ? new ReportPeriod(months, today) : ReportingErrors.InvalidPeriod;

    /// <summary>Khóa tháng dạng <c>YYYY-MM</c> (giống frontend)</summary>
    public static string KeyOf(DateOnly month) => $"{month.Year:D4}-{month.Month:D2}";

    /// <summary>Nhãn ngắn trên trục: "T5"; tháng khác năm hiện tại thêm năm 2 số — "T12/25"</summary>
    public string LabelOf(DateOnly month) => month.Year == Today.Year ? $"T{month.Month}" : $"T{month.Month}/{month.Year % 100:D2}";

    /// <summary>Ngày (giờ VN) có nằm trong kỳ</summary>
    public bool Contains(DateOnly day) => day >= From && day <= To;
}
