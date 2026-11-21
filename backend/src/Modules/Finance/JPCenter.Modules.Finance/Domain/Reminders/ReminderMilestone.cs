namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>Mốc tự nhắc học phí (TC-11 bước 4) — mỗi hóa đơn mỗi mốc 1 lần</summary>
public enum ReminderMilestone
{
    /// <summary>Trước hạn N ngày</summary>
    BeforeDue,
    /// <summary>Đúng hạn</summary>
    OnDue,
    /// <summary>Quá hạn N ngày</summary>
    Overdue,
}
