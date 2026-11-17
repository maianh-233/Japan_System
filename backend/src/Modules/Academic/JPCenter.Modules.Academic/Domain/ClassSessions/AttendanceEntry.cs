namespace JPCenter.Modules.Academic.Domain.ClassSessions;

/// <summary>1 dòng điểm danh nhập tay (GV / nhân viên) — DT-09</summary>
/// <param name="StudentId">Students.Student</param>
/// <param name="LeftAt">Về sớm lúc (vẫn tính có mặt)</param>
public sealed record AttendanceEntry(
    Guid StudentId,
    AttendanceStatus Status,
    TimeOnly? CheckInAt,
    string? Note,
    TimeOnly? LeftAt = null,
    string? LeaveReason = null);
