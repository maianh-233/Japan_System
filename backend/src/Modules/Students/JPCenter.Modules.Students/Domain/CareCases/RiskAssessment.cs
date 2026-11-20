namespace JPCenter.Modules.Students.Domain.CareCases;

/// <summary>
/// Kết quả tính nguy cơ bỏ học (HV-14 bước 1–2) — khớp <c>studentInsights.ts</c> / <c>atRisk.ts › riskLevel</c> ở frontend.
/// Dấu hiệu nặng (vắng nhiều, vắng liên tiếp, thi kém / vắng thi) = 3 điểm, nhẹ = 1 điểm.
/// Mức: cao (≥ HighFrom hoặc vắng liên tiếp ≥ 3) · trung bình (≥ MediumFrom) · thấp. Không có dấu hiệu → <see cref="Level"/> null.
/// </summary>
public sealed record RiskAssessment(RiskLevel? Level, IReadOnlyList<string> Flags, int Score)
{
    public const string Absent = "absent", Streak = "streak", Rate = "rate", Submit = "submit", LowScore = "score", Exam = "exam", Tuition = "tuition";

    public bool Flagged => Flags.Count > 0;

    public static RiskAssessment Evaluate(RiskSignals s, RiskThresholds t)
    {
        var flags = new List<string>();
        var score = 0;
        void Add(string key, bool heavy, int times = 1)
        {
            for (var i = 0; i < times; i++) flags.Add(key);
            score += (heavy ? 3 : 1) * times;
        }

        if (s.Absent >= t.Absent) Add(Absent, true);
        if (s.Streak >= t.Streak) Add(Streak, true);
        if (s.AttendanceRate is { } rate && s.Taken >= RiskThresholds.MinSessionsForRate && rate < t.AttendanceRate) Add(Rate, false);
        if (s.Due >= RiskThresholds.MinDueForSubmitRate && s.Submitted * 100 < t.SubmitRate * s.Due) Add(Submit, false);
        if (s.AssignmentAvgPct is { } avg && avg < t.ScorePct) Add(LowScore, false);
        if (s.WeakExams > 0) Add(Exam, true, s.WeakExams);
        if (s.OverdueDebt) Add(Tuition, false);

        RiskLevel? level = flags.Count == 0 ? null
            : score >= t.HighFrom || s.Streak >= RiskThresholds.HighStreak ? RiskLevel.High
            : score >= t.MediumFrom ? RiskLevel.Medium
            : RiskLevel.Low;
        return new RiskAssessment(level, [.. flags.Distinct()], score);
    }
}
