namespace JPCenter.Modules.System.Contracts.Dtos;

/// <summary>Ngưỡng cảnh báo bỏ học (HV-14) — % là số nguyên 0 → 100</summary>
public sealed record AtRiskThresholds(int Absent, int Streak, int AttendanceRate, int SubmitRate, int ScorePct, int MediumFrom, int HighFrom);
