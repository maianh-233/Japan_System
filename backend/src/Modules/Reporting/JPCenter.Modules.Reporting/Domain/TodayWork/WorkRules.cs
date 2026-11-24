using System.Globalization;

namespace JPCenter.Modules.Reporting.Domain.TodayWork;

/// <summary>
/// Quy tắc chọn + xếp mức độ việc hôm nay (BC-02). Mọi thời điểm là <b>giờ Việt Nam</b> (không múi giờ) — đổi ở tầng Application.
/// Trả null = không phải việc cần làm hôm nay.
/// </summary>
public static class WorkRules
{
    /// <summary>Nhắc lead chưa liên hệ khi thời gian SLA còn lại ≤ 30%</summary>
    public const decimal NearSlaRatio = 0.3m;

    /// <summary>Buổi học quá số ngày này vẫn chưa điểm danh thì không nhắc nữa</summary>
    public const int AttendanceLookbackDays = 7;

    /// <summary>Hóa đơn "đến hạn" = hạn trong số ngày tới này</summary>
    public const int DueSoonDays = 3;

    /// <summary>Đơn chờ duyệt có buổi học liên quan trong số giờ tới này → đỏ</summary>
    public const int RequestUrgentHours = 24;

    /// <summary>Giao dịch chưa khớp từ số ngày này → đỏ</summary>
    public const int UnmatchedDangerDays = 2;

    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    /// <summary>Lead <c>new</c> chưa liên hệ lần nào: đã / sắp quá SLA liên hệ lần đầu</summary>
    public static WorkUrgency? LeadSla(DateTime createdAt, int slaMinutes, DateTime now)
    {
        var elapsed = (int)Math.Floor((now - createdAt).TotalMinutes);
        var left = slaMinutes - elapsed;
        if (left > slaMinutes * NearSlaRatio) return null;
        return left <= 0
            ? new WorkUrgency(WorkTone.Danger, $"quá {HumanDuration.Of(-left)}")
            : new WorkUrgency(WorkTone.Warning, $"còn {HumanDuration.Of(left)}");
    }

    /// <summary>Lead đang mở có hẹn gọi lại trước cuối ngày hôm nay</summary>
    public static WorkUrgency? FollowUp(DateTime followUpAt, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var day = DateOnly.FromDateTime(followUpAt);
        if (day > today) return null;
        if (day < today) return new WorkUrgency(WorkTone.Danger, $"trễ hẹn {today.DayNumber - day.DayNumber} ngày");
        return new WorkUrgency(followUpAt < now ? WorkTone.Warning : WorkTone.Info, $"hẹn {followUpAt.ToString("HH:mm", Vi)}");
    }

    /// <summary>Buổi học không hủy, đã bắt đầu, trong 7 ngày qua mà chưa điểm danh</summary>
    public static WorkUrgency? MissingAttendance(DateOnly date, TimeOnly start, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        if (date > today || date < today.AddDays(-AttendanceLookbackDays)) return null;
        if (date.ToDateTime(start) >= now) return null;
        return date == today
            ? new WorkUrgency(WorkTone.Warning, "hôm nay")
            : new WorkUrgency(WorkTone.Danger, $"{today.DayNumber - date.DayNumber} ngày trước");
    }

    /// <summary>Đơn chờ người dùng duyệt; <paramref name="sessionStart"/> = giờ bắt đầu buổi học liên quan (đơn Hr không có)</summary>
    public static WorkUrgency PendingRequest(DateTime submittedAt, DateTime? sessionStart, DateTime now) =>
        sessionStart is { } s && (s - now).TotalHours < RequestUrgentHours
            ? new WorkUrgency(WorkTone.Danger, $"buổi học < {RequestUrgentHours} giờ")
            : new WorkUrgency(WorkTone.Warning, $"gửi {submittedAt.ToString("dd/MM", Vi)}");

    /// <summary>Hóa đơn chưa hủy còn nợ: hạn ≤ hôm nay + 3 ngày</summary>
    public static WorkUrgency? InvoiceDue(DateOnly dueDate, DateOnly today)
    {
        if (dueDate > today.AddDays(DueSoonDays)) return null;
        var late = today.DayNumber - dueDate.DayNumber;
        return late switch
        {
            > 0 => new WorkUrgency(WorkTone.Danger, $"quá hạn {late} ngày"),
            0 => new WorkUrgency(WorkTone.Warning, "hạn hôm nay"),
            _ => new WorkUrgency(WorkTone.Info, $"hạn {dueDate.ToString("dd/MM", Vi)}"),
        };
    }

    /// <summary>Giao dịch ngân hàng chưa khớp hóa đơn</summary>
    public static WorkUrgency UnmatchedTransaction(DateTime at, DateTime now)
    {
        var age = Math.Max(0, (int)Math.Floor((now - at).TotalDays));
        var tone = age >= UnmatchedDangerDays ? WorkTone.Danger : WorkTone.Warning;
        return new WorkUrgency(tone, age > 0 ? $"{age} ngày trước" : at.ToString("HH:mm", Vi));
    }
}
