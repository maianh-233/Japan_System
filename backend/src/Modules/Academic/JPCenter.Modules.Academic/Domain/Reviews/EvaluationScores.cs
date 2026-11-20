namespace JPCenter.Modules.Academic.Domain.Reviews;

/// <summary>Điểm sao nhận xét cuối khóa: 5 kỹ năng + thái độ & chuyên cần, mỗi mục 1–5</summary>
public sealed record EvaluationScores(int Vocab, int Grammar, int Reading, int Listening, int Speaking, int Attitude)
{
    public bool IsValid => new[] { Vocab, Grammar, Reading, Listening, Speaking, Attitude }.All(s => s is >= 1 and <= 5);

    /// <summary>Trung bình 5 kỹ năng (không tính thái độ)</summary>
    public decimal SkillAverage => Math.Round((Vocab + Grammar + Reading + Listening + Speaking) / 5m, 1);
}
