namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>
/// Lịch tự nhắc học phí (TC-11 bước 4): trước hạn <see cref="DaysBefore"/> ngày · đúng hạn · quá hạn <see cref="DaysAfter"/> ngày.
/// Job hằng ngày lấy MỐC MUỘN NHẤT đã tới mà chưa nhắc — job lỡ 1 ngày vẫn nhắc bù, không gửi lại mốc cũ hơn.
/// </summary>
public sealed record ReminderPlan(int DaysBefore = 3, int DaysAfter = 7)
{
    /// <summary>Mốc cần nhắc hôm nay cho hóa đơn hạn <paramref name="dueDate"/>; null = chưa tới mốc nào / đã nhắc mốc mới nhất</summary>
    public ReminderMilestone? Due(DateOnly dueDate, DateOnly today, IReadOnlyCollection<ReminderMilestone> alreadySent)
    {
        ReminderMilestone? latest = today >= dueDate.AddDays(DaysAfter) ? ReminderMilestone.Overdue
            : today >= dueDate ? ReminderMilestone.OnDue
            : today >= dueDate.AddDays(-DaysBefore) ? ReminderMilestone.BeforeDue
            : null;
        return latest is { } m && !alreadySent.Contains(m) ? m : null;
    }
}
