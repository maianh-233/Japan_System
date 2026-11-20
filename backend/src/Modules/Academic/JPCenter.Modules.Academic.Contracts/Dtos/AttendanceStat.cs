namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <summary>Chuyên cần 1 HV trong 1 lớp</summary>
/// <param name="Taken">Số buổi đã điểm danh</param>
/// <param name="Absent">Vắng không phép (chưa học bù)</param>
/// <param name="Streak">Số buổi vắng không phép liên tiếp gần nhất</param>
/// <param name="AttendanceRate">% chuyên cần (có mặt + muộn + đã học bù) — null khi chưa điểm danh buổi nào</param>
public sealed record AttendanceStat(Guid ClassId, Guid StudentId, int Taken, int Absent, int Streak, int? AttendanceRate);
