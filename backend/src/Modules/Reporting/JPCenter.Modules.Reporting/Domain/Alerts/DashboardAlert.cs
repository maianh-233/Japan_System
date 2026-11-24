using JPCenter.Modules.Reporting.Domain.TodayWork;

namespace JPCenter.Modules.Reporting.Domain.Alerts;

/// <summary>1 dòng "Cần xử lý" ở bảng điều khiển (BC-01 bước 4) — chỉ tạo khi có việc (số &gt; 0).</summary>
public sealed record DashboardAlert(DashboardAlertKind Kind, WorkTone Tone, string Text, string Path)
{
    /// <summary>Buổi đã dạy chưa xác nhận: xét trong số ngày qua này</summary>
    public const int UnconfirmedLookbackDays = 14;

    /// <summary>Buổi chưa có nhật ký GV: xét trong số ngày qua này</summary>
    public const int MissingLogLookbackDays = 7;

    /// <summary>Lớp lấp đầy dưới % này là lớp vắng</summary>
    public const int LowFillPercent = 50;

    /// <summary>Số mã lớp vắng nêu tên trong câu cảnh báo</summary>
    public const int LowFillSampleCount = 3;

    public static DashboardAlert? LeadSlaOverdue(int count) =>
        count > 0 ? new(DashboardAlertKind.LeadSlaOverdue, WorkTone.Danger, $"{count} lead mới quá hạn liên hệ (SLA)", "/admin/crm/lead-rules") : null;

    public static DashboardAlert? UnconfirmedSessions(int count) =>
        count > 0
            ? new(DashboardAlertKind.UnconfirmedSessions, WorkTone.Warning, $"{count} buổi đã dạy chưa xác nhận (ảnh hưởng lương GV)", "/admin/academic/sessions")
            : null;

    public static DashboardAlert? MissingTeacherLogs(int count) =>
        count > 0
            ? new(DashboardAlertKind.MissingTeacherLogs, WorkTone.Warning, $"{count} buổi tuần qua GV chưa ghi nhật ký", "/admin/academic/sessions")
            : null;

    /// <param name="lowFillClassCodes">Mã lớp lấp đầy dưới ngưỡng, vắng nhất trước</param>
    public static DashboardAlert? LowFillClasses(IReadOnlyList<string> lowFillClassCodes) =>
        lowFillClassCodes.Count > 0
            ? new(DashboardAlertKind.LowFillClasses, WorkTone.Warning,
                $"{lowFillClassCodes.Count} lớp lấp đầy dưới {LowFillPercent}%: {string.Join(", ", lowFillClassCodes.Take(LowFillSampleCount))}",
                "/admin/academic/classes")
            : null;

    /// <param name="name">Tên tích hợp</param>
    /// <param name="error">Thông báo lỗi gần nhất — chỉ lấy phần trước dấu "—"</param>
    public static DashboardAlert IntegrationError(string name, string? error)
    {
        var reason = (error ?? string.Empty).Split('—')[0].Trim();
        var text = reason.Length == 0 ? $"Tích hợp {name} lỗi" : $"Tích hợp {name} lỗi: {reason}";
        return new(DashboardAlertKind.IntegrationError, WorkTone.Danger, text, "/admin/system/integrations");
    }

    /// <summary>Lớp lấp đầy dưới ngưỡng (lớp chưa có sĩ số tối đa → null → không tính)</summary>
    public static bool IsLowFill(int? fillPercent) => fillPercent < LowFillPercent;
}
