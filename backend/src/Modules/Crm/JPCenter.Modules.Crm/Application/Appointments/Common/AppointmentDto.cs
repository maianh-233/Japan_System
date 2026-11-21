using JPCenter.Modules.Crm.Domain.Appointments;

namespace JPCenter.Modules.Crm.Application.Appointments.Common;

/// <summary>Lịch hẹn — khớp <c>Appointment</c> frontend (<c>features/crm/appointments/appointments.ts</c>) + tên lead / phòng / người tiếp</summary>
public sealed record AppointmentDto(Guid Id, string Code, Guid LeadId, string LeadName, string LeadPhone, AppointmentKind Kind, Guid BranchId,
    Guid? RoomId, string? RoomName, Guid StaffId, string StaffName, DateTimeOffset Start, DateTimeOffset End, AppointmentStatus Status,
    string? Note, string? Outcome, DateTimeOffset CreatedAt, DateTimeOffset? RemindedAt, string? CancelReason, uint Version);

/// <summary>Chỗ trùng (CRM-14) — <c>kind</c>: room · staff · lead</summary>
public sealed record ConflictDto(ConflictKind Kind, string Message);
