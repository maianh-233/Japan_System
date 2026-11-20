using System.Globalization;
using JPCenter.Modules.System.Domain.AuditLogs;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>
/// Bộ Cài đặt chung của trung tâm (CD-01 → 03) — khớp <c>CenterSettings</c> ở <c>frontend/src/features/system/settings.ts</c>.
/// Lưu phẳng mỗi mục 1 dòng <see cref="Setting"/> (khóa "nhóm.mục"); mục chưa lưu lấy theo <see cref="Default"/>.
/// </summary>
public sealed record CenterSettings(
    CrmSettings Crm,
    OfficeSettings Office,
    InvoiceSettings Invoice,
    PayrollSettings Payroll,
    AtRiskSettings AtRisk,
    ParentNoticeSettings Parent,
    SafetySettings Safety)
{
    /// <summary>Giá trị mặc định — trùng <c>DEFAULT_SETTINGS</c> của frontend</summary>
    public static readonly CenterSettings Default = new(
        new CrmSettings(15),
        new OfficeSettings(new(8, 0), new(17, 30), new(12, 0), 5),
        new InvoiceSettings("HD-{CN}-{YY}{MM}-{SO:4}", InvoiceNumberReset.Month),
        new PayrollSettings(5),
        new AtRiskSettings(Absent: 3, Streak: 2, AttendanceRate: 80, SubmitRate: 50, ScorePct: 60, MediumFrom: 3, HighFrom: 5),
        new ParentNoticeSettings(ArrivalNotice: true, AbsentAlertMin: 15, UnderAge: 0, WeeklyReport: true, WeeklyDay: 0, WeeklyTime: new(20, 0)),
        new SafetySettings(Money.Zero, 30));

    /// <summary>Nhãn tiếng Việt từng mục — trùng <c>SETTING_LABELS</c> của frontend (dùng cho nhật ký thay đổi)</summary>
    public static readonly IReadOnlyDictionary<string, string> Labels = new Dictionary<string, string>
    {
        ["crm.defaultSlaMinutes"] = "SLA liên hệ lead mặc định (phút)",
        ["office.from"] = "Giờ hành chính — bắt đầu",
        ["office.to"] = "Giờ hành chính — kết thúc",
        ["office.satTo"] = "Thứ 7 làm đến",
        ["office.lateGraceMin"] = "Châm chước đi muộn (phút)",
        ["invoice.codePattern"] = "Mẫu mã hóa đơn",
        ["invoice.resetEvery"] = "Đánh số lại hóa đơn",
        ["payroll.lockDay"] = "Ngày khóa bảng lương",
        ["atRisk.absent"] = "Bỏ học — vắng không phép từ",
        ["atRisk.streak"] = "Bỏ học — vắng liên tiếp từ",
        ["atRisk.attendanceRate"] = "Bỏ học — chuyên cần dưới (%)",
        ["atRisk.submitRate"] = "Bỏ học — nộp bài dưới (%)",
        ["atRisk.scorePct"] = "Bỏ học — điểm dưới (%)",
        ["atRisk.mediumFrom"] = "Bỏ học — điểm rủi ro mức Trung bình",
        ["atRisk.highFrom"] = "Bỏ học — điểm rủi ro mức Cao",
        ["parent.arrivalNotice"] = "Báo phụ huynh khi con đến lớp",
        ["parent.absentAlertMin"] = "Báo chưa đến lớp sau (phút)",
        ["parent.underAge"] = "Chỉ báo cho học viên dưới (tuổi)",
        ["parent.weeklyReport"] = "Tự gửi báo cáo tuần",
        ["parent.weeklyDay"] = "Ngày gửi báo cáo tuần",
        ["parent.weeklyTime"] = "Giờ gửi báo cáo tuần",
        ["safety.refundApprovalMin"] = "Hoàn tiền cần duyệt từ (đ)",
        ["safety.trashDays"] = "Giữ trong Thùng rác (ngày)",
    };

    private static readonly string[] WeekDays = ["Chủ nhật", "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7"];

    /// <summary>
    /// Kiểm tra cả bộ (CD-02): sai khoảng → <c>validation.failed</c> kèm lỗi theo khóa "nhóm.mục" (E1);
    /// đủ khoảng thì kiểm tra ràng buộc giữa các mục (E2 → E4).
    /// </summary>
    public Result Validate()
    {
        var errors = new Dictionary<string, string[]>();
        void Range(string key, int value, int min, int max)
        {
            if (value < min || value > max) errors[key] = [$"{Labels[key]}: từ {min} đến {max}"];
        }

        Range("crm.defaultSlaMinutes", Crm.DefaultSlaMinutes, 1, 1440);
        Range("office.lateGraceMin", Office.LateGraceMin, 0, 120);
        Range("payroll.lockDay", Payroll.LockDay, 1, 28);
        Range("atRisk.absent", AtRisk.Absent, 1, 20);
        Range("atRisk.streak", AtRisk.Streak, 1, 20);
        Range("atRisk.attendanceRate", AtRisk.AttendanceRate, 0, 100);
        Range("atRisk.submitRate", AtRisk.SubmitRate, 0, 100);
        Range("atRisk.scorePct", AtRisk.ScorePct, 0, 100);
        Range("atRisk.mediumFrom", AtRisk.MediumFrom, 1, 20);
        Range("atRisk.highFrom", AtRisk.HighFrom, 1, 20);
        Range("parent.absentAlertMin", Parent.AbsentAlertMin, 0, 120);
        Range("parent.underAge", Parent.UnderAge, 0, 25);
        Range("parent.weeklyDay", Parent.WeeklyDay, 0, 6);
        Range("safety.trashDays", Safety.TrashDays, 1, 365);
        if (!Enum.IsDefined(Invoice.ResetEvery)) errors["invoice.resetEvery"] = ["Đánh số lại hóa đơn: không bao giờ / mỗi năm / mỗi tháng"];
        if (errors.Count > 0) return Error.InvalidInput(errors);

        if (Office.From >= Office.To || (Office.SatTo is { } sat && sat <= Office.From)) return SettingsErrors.OfficeHoursInvalid;
        if (!Invoice.IsPatternValid()) return SettingsErrors.InvoicePatternInvalid;
        if (AtRisk.MediumFrom >= AtRisk.HighFrom) return SettingsErrors.AtRiskLevelsInvalid;
        return Result.Success();
    }

    /// <summary>Phẳng hóa thành từng mục "nhóm.mục" theo đúng thứ tự <see cref="Labels"/>.</summary>
    public IReadOnlyList<SettingEntry> ToEntries() =>
    [
        Num("crm.defaultSlaMinutes", Crm.DefaultSlaMinutes),
        Time("office.from", Office.From),
        Time("office.to", Office.To),
        new("office.satTo", Office.SatTo?.ToString("HH:mm", CultureInfo.InvariantCulture), Office.SatTo?.ToString("HH:mm", CultureInfo.InvariantCulture) ?? "Nghỉ"),
        Num("office.lateGraceMin", Office.LateGraceMin),
        new("invoice.codePattern", Invoice.CodePattern, Invoice.CodePattern),
        new("invoice.resetEvery", ResetCode(Invoice.ResetEvery), ResetLabel(Invoice.ResetEvery)),
        Num("payroll.lockDay", Payroll.LockDay),
        Num("atRisk.absent", AtRisk.Absent),
        Num("atRisk.streak", AtRisk.Streak),
        Num("atRisk.attendanceRate", AtRisk.AttendanceRate),
        Num("atRisk.submitRate", AtRisk.SubmitRate),
        Num("atRisk.scorePct", AtRisk.ScorePct),
        Num("atRisk.mediumFrom", AtRisk.MediumFrom),
        Num("atRisk.highFrom", AtRisk.HighFrom),
        Flag("parent.arrivalNotice", Parent.ArrivalNotice),
        Num("parent.absentAlertMin", Parent.AbsentAlertMin),
        Num("parent.underAge", Parent.UnderAge),
        Flag("parent.weeklyReport", Parent.WeeklyReport),
        new("parent.weeklyDay", Parent.WeeklyDay, Parent.WeeklyDay is >= 0 and <= 6 ? WeekDays[Parent.WeeklyDay] : Parent.WeeklyDay.ToString()),
        Time("parent.weeklyTime", Parent.WeeklyTime),
        new("safety.refundApprovalMin", (long)Safety.RefundApprovalMin.Amount, Safety.RefundApprovalMin.ToString()),
        Num("safety.trashDays", Safety.TrashDays),
    ];

    /// <summary>Các mục khác nhau giữa bộ này (trước) và <paramref name="next"/> (sau) — ghi nhật ký "Đổi cài đặt chung".</summary>
    public List<AuditChange> Diff(CenterSettings next)
    {
        var before = ToEntries().ToDictionary(e => e.Key);
        return next.ToEntries()
            .Where(after => !Equals(before[after.Key].Value, after.Value))
            .Select(after => new AuditChange(Labels[after.Key], before[after.Key].Display, after.Display))
            .ToList();
    }

    /// <summary>Mã lưu của kiểu đánh số lại — khớp frontend ('never' | 'year' | 'month')</summary>
    public static string ResetCode(InvoiceNumberReset reset) => reset switch
    {
        InvoiceNumberReset.Year => "year",
        InvoiceNumberReset.Month => "month",
        _ => "never",
    };

    private static string ResetLabel(InvoiceNumberReset reset) => reset switch
    {
        InvoiceNumberReset.Year => "Mỗi năm",
        InvoiceNumberReset.Month => "Mỗi tháng",
        _ => "Không đánh số lại",
    };

    private static SettingEntry Num(string key, int value) => new(key, value, value.ToString());
    private static SettingEntry Time(string key, TimeOnly value) => new(key, value.ToString("HH:mm", CultureInfo.InvariantCulture), value.ToString("HH:mm", CultureInfo.InvariantCulture));
    private static SettingEntry Flag(string key, bool value) => new(key, value, value ? "Bật" : "Tắt");
}
