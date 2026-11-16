namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Trạng thái buổi học</summary>
public enum SessionStatus
{
    /// <summary>Đã lên lịch</summary>
    Scheduled,
    /// <summary>Đã diễn ra</summary>
    Completed,
    /// <summary>Đã hủy</summary>
    Cancelled,
}
