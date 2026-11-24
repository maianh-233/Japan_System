namespace JPCenter.Modules.Academic.Contracts.Dtos.Reports;

/// <summary>Buổi chưa hủy, chưa điểm danh, của lớp chưa hủy (BC-02 Giáo vụ).</summary>
public sealed record SessionWithoutAttendance(Guid SessionId, Guid ClassId, string ClassCode, DateOnly Date, TimeOnly Start, TimeOnly End, bool Exam);
