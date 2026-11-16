namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Loại hỗ trợ của nhân viên trong buổi</summary>
public enum SupportKind
{
    /// <summary>Điểm danh hộ</summary>
    Attendance,
    /// <summary>HV về sớm</summary>
    EarlyLeave,
    /// <summary>Chấm công hộ GV</summary>
    TeacherCheckin,
    /// <summary>Sự cố</summary>
    Incident,
    /// <summary>Phòng / thiết bị</summary>
    Room,
    /// <summary>Khác</summary>
    Other,
}
