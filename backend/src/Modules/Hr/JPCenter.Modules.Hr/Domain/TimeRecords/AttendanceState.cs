namespace JPCenter.Modules.Hr.Domain.TimeRecords;

/// <summary>Trạng thái chấm công 1 ngày (tính khi đọc, không lưu) — khớp `DayState` frontend</summary>
public enum AttendanceState
{
    /// <summary>Đúng giờ / có công</summary>
    Ok,
    /// <summary>Đi muộn</summary>
    Late,
    /// <summary>Về sớm</summary>
    Early,
    /// <summary>Muộn + về sớm</summary>
    LateEarly,
    /// <summary>Quên check-out</summary>
    NoOut,
    /// <summary>Vắng không phép</summary>
    Absent,
    /// <summary>Nghỉ phép / công tác đã duyệt</summary>
    Leave,
    /// <summary>Không có giờ phải làm</summary>
    Off,
    /// <summary>Chưa tới</summary>
    Future,
    /// <summary>Đang làm (hôm nay, chưa check-out)</summary>
    Working,
}
