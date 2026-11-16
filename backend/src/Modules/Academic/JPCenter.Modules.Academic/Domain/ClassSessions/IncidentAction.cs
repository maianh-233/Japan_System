namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Cách xử lý sự cố</summary>
public enum IncidentAction
{
    /// <summary>Tạm dừng lớp chờ xử lý</summary>
    Pause,
    /// <summary>GV khác dạy tiếp</summary>
    Substitute,
    /// <summary>Trợ giảng dạy tiếp</summary>
    Assistant,
    /// <summary>Kết thúc sớm, xếp bù phần còn lại</summary>
    EndEarly,
}
