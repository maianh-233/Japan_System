using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Kết quả chấm 1 lượt làm test (CRM-13 bước 3) — giống <c>placement.ts › grade</c>.</summary>
public sealed record PlacementScore(int Correct, int Total, Dictionary<JapaneseSkill, SkillScore> BySkill)
{
    /// <summary>% điểm (0–100, làm tròn 2 chữ số)</summary>
    public decimal Percent => Total == 0 ? 0 : Math.Round(Correct * 100m / Total, 2);

    /// <summary>
    /// Chấm theo danh sách câu CỐ ĐỊNH của đề. Câu không còn trong ngân hàng (thiếu khóa) bị bỏ khỏi tổng;
    /// câu bỏ trống (-1) hoặc không trả lời tính sai.
    /// </summary>
    public static PlacementScore Grade(IReadOnlyList<Guid> questionIds, IReadOnlyDictionary<Guid, AnswerKey> keys,
        IReadOnlyDictionary<Guid, int> answers)
    {
        var bySkill = new Dictionary<JapaneseSkill, SkillScore>();
        var (correct, total) = (0, 0);
        foreach (var id in questionIds)
        {
            if (!keys.TryGetValue(id, out var key)) continue;
            var ok = answers.TryGetValue(id, out var chosen) && chosen == key.Answer;
            total++;
            if (ok) correct++;
            var s = bySkill.GetValueOrDefault(key.Skill) ?? new SkillScore(0, 0);
            bySkill[key.Skill] = new SkillScore(s.Correct + (ok ? 1 : 0), s.Total + 1);
        }
        return new PlacementScore(correct, total, bySkill);
    }
}
