namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Lý do sự cố của GV giữa buổi</summary>
public enum IncidentReason
{
    /// <summary>Ngất / choáng</summary>
    Faint,
    /// <summary>Ốm</summary>
    Sick,
    /// <summary>Việc gia đình</summary>
    Family,
    /// <summary>Tai nạn</summary>
    Accident,
    /// <summary>Sự cố kỹ thuật</summary>
    Tech,
    /// <summary>Khác</summary>
    Other,
}
