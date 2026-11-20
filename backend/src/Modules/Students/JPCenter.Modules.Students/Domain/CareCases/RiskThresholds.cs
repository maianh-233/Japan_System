namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>Ngưỡng cảnh báo bỏ học — Cài đặt chung <c>atRisk</c> (giống frontend <c>features/system/settings</c>)</summary>
public sealed record RiskThresholds(
    int Absent = 3,
    int Streak = 2,
    int AttendanceRate = 80,
    int SubmitRate = 50,
    int ScorePct = 60,
    int MediumFrom = 3,
    int HighFrom = 5)
{
    public static readonly RiskThresholds Default = new();

    /// <summary>Chuyên cần chỉ xét khi đã học ≥ N buổi</summary>
    public const int MinSessionsForRate = 4;
    /// <summary>Tỉ lệ nộp bài chỉ xét khi đã có ≥ N bài tới hạn</summary>
    public const int MinDueForSubmitRate = 2;
    /// <summary>Vắng liên tiếp từ N buổi → nguy cơ cao ngay</summary>
    public const int HighStreak = 3;
}
