namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>Trạng thái lịch hẹn</summary>
public enum AppointmentStatus
{
    /// <summary>Đã hẹn</summary>
    Scheduled,
    /// <summary>Đã đến</summary>
    Done,
    /// <summary>Không đến</summary>
    NoShow,
    /// <summary>Đã hủy</summary>
    Cancelled,
}
