namespace JPCenter.Modules.System.Domain.Settings;

/// <summary>Ngưỡng cảnh báo bỏ học (HV-14)</summary>
/// <param name="Absent">Vắng không phép từ N buổi — 1 → 20</param>
/// <param name="Streak">Vắng liên tiếp từ N buổi — 1 → 20</param>
/// <param name="AttendanceRate">Chuyên cần dưới N% — 0 → 100</param>
/// <param name="SubmitRate">Tỉ lệ nộp bài dưới N% — 0 → 100</param>
/// <param name="ScorePct">Điểm dưới N% — 0 → 100</param>
/// <param name="MediumFrom">Tổng điểm rủi ro (dấu hiệu nặng = 3, nhẹ = 1) từ N → mức Trung bình</param>
/// <param name="HighFrom">… từ N → mức Cao — lớn hơn <paramref name="MediumFrom"/></param>
public sealed record AtRiskSettings(int Absent, int Streak, int AttendanceRate, int SubmitRate, int ScorePct, int MediumFrom, int HighFrom);
