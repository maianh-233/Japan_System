using JPCenter.SharedKernel.Events;

namespace JPCenter.Modules.Academic.Contracts.IntegrationEvents;

/// <summary>Hủy 1 buổi (DT-11) — Notifications báo HV / PH, Students đóng đơn xin nghỉ của buổi.</summary>
public sealed record SessionCancelled(Guid SessionId, Guid ClassId, DateOnly Date, string Reason) : IIntegrationEvent;

/// <summary>Dời lịch / đổi GV / đổi phòng 1 buổi (DT-10) — Notifications báo HV / PH và GV.</summary>
public sealed record SessionRescheduled(Guid SessionId, Guid ClassId, DateOnly Date, TimeOnly StartTime, TimeOnly EndTime,
    IReadOnlyList<Guid> TeacherIds, string Reason) : IIntegrationEvent;

/// <summary>Người đứng lớp được tính công trong buổi đã xác nhận. <paramref name="Role"/>: teacher · substitute · assistant</summary>
public sealed record ConfirmedStaff(Guid EmployeeId, string Role);

/// <summary>Xác nhận buổi đã dạy (DT-15) — Finance tính lương GV / trợ giảng theo buổi.</summary>
public sealed record SessionConfirmed(Guid SessionId, Guid ClassId, Guid BranchId, DateOnly Date, bool Online,
    IReadOnlyList<ConfirmedStaff> Staff) : IIntegrationEvent;

/// <summary>Bỏ xác nhận (DT-15) — Finance bỏ buổi khỏi bảng lương chưa chốt.</summary>
public sealed record SessionUnconfirmed(Guid SessionId, Guid ClassId, DateOnly Date) : IIntegrationEvent;

/// <summary>Lưu điểm danh HV của 1 buổi (DT-09) — Students tính lại nguy cơ bỏ học cho các HV này (HV-14.E6), Notifications báo PH.</summary>
public sealed record AttendanceRecorded(Guid SessionId, Guid ClassId, Guid BranchId, DateOnly Date, IReadOnlyList<Guid> StudentIds) : IIntegrationEvent;

/// <summary>GV nhận lời mời dạy thay (DT-16) — Notifications báo HV / PH đổi GV.</summary>
public sealed record SubstituteAccepted(Guid SessionId, Guid ClassId, Guid FromTeacherId, Guid ToTeacherId) : IIntegrationEvent;
