namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Kết quả sau khi tạm dừng</summary>
public enum IncidentOutcome
{
    /// <summary>Lớp học tiếp</summary>
    Resumed,
    /// <summary>Cho nghỉ luôn</summary>
    Ended,
}
