namespace JPCenter.Modules.Crm.Domain.Appointments;

/// <summary>1 chỗ trùng khi đặt / đổi lịch hẹn — trả về giao diện trong <c>conflicts</c></summary>
public sealed record AppointmentConflict(ConflictKind Kind, string Message);
