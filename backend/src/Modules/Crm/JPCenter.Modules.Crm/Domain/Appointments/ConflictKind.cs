namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>Loại trùng lịch hẹn (CRM-14)</summary>
public enum ConflictKind
{
    /// <summary>Phòng bảo trì / ngừng dùng / có hẹn khác / có buổi học</summary>
    Room,
    /// <summary>Người tiếp có hẹn khác / đang dạy / nghỉ dài ngày</summary>
    Staff,
    /// <summary>Lead đã có hẹn khác giao giờ</summary>
    Lead,
}
