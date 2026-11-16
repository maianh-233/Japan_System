namespace JPCenter.Modules.Academic.Domain.ClassSessions.Events;

/// <summary>Người đứng lớp được tính công trong buổi đã xác nhận</summary>
public sealed record ConfirmedTeacher(Guid TeacherId, StaffRole Role);
