namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>
/// Số liệu học tập của 1 HV trong 1 lớp để tính nguy cơ bỏ học (HV-14) — handler gom từ Academic.Contracts / Finance.Contracts.
/// </summary>
/// <param name="Taken">Số buổi đã điểm danh</param>
/// <param name="Absent">Vắng không phép</param>
/// <param name="Streak">Vắng liên tiếp gần nhất</param>
/// <param name="AttendanceRate">% chuyên cần (có mặt + muộn + đã học bù) — null khi chưa điểm danh buổi nào</param>
/// <param name="Due">Bài tập đã tới hạn</param>
/// <param name="Submitted">Bài đã nộp trong số tới hạn</param>
/// <param name="AssignmentAvgPct">Điểm bài tập TB (%)</param>
/// <param name="WeakExams">Số bài thi vắng / dưới ngưỡng điểm</param>
/// <param name="OverdueDebt">Có hóa đơn quá hạn</param>
public sealed record RiskSignals(
    int Taken,
    int Absent,
    int Streak,
    int? AttendanceRate,
    int Due,
    int Submitted,
    int? AssignmentAvgPct,
    int WeakExams,
    bool OverdueDebt);
