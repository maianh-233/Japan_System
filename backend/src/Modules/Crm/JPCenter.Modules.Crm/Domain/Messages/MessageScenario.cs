namespace JPCenter.Modules.Crm.Domain.Messages;

/// <summary>Tình huống dùng mẫu tin</summary>
public enum MessageScenario
{
    /// <summary>Chào lead mới</summary>
    Welcome,
    /// <summary>Nhắc lịch test</summary>
    TestReminder,
    /// <summary>Nhắc lịch tư vấn</summary>
    ConsultReminder,
    /// <summary>Nhắc học phí</summary>
    Tuition,
    /// <summary>Báo nghỉ học</summary>
    ClassOff,
    /// <summary>Báo con đã đến lớp</summary>
    Arrival,
    /// <summary>Báo con chưa đến lớp</summary>
    AbsentAlert,
    /// <summary>Báo cáo tuần</summary>
    WeeklyReport,
    /// <summary>Tự soạn</summary>
    Custom,
}
