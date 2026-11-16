namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>Điểm danh học viên</summary>
public enum AttendanceStatus
{
    /// <summary>Có mặt</summary>
    Present,
    /// <summary>Đi muộn (tính có mặt)</summary>
    Late,
    /// <summary>Vắng có phép</summary>
    Excused,
    /// <summary>Vắng không phép</summary>
    Absent,
}
