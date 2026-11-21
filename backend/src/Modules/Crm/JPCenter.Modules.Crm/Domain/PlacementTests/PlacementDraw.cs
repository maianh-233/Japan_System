using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.PlacementTests;

/// <summary>Bốc câu cho bài test xếp lớp (CRM-12) — giống <c>placement.ts › pickQuestions</c>.</summary>
public static class PlacementDraw
{
    /// <summary>
    /// Bốc <c>Count</c> câu cho từng phần từ kho câu ĐÃ DUYỆT đúng cấp độ + kỹ năng (Application đọc qua Academic.Contracts).
    /// <paramref name="random"/> null = lấy theo thứ tự kho; có = bốc ngẫu nhiên ("Đổi đề"). Thiếu câu → lỗi liệt kê từng phần.
    /// </summary>
    public static Result<List<Guid>> Draw(IReadOnlyList<PlacementSection> sections, Func<JlptLevel, JapaneseSkill, IReadOnlyList<Guid>> approvedPool,
        Random? random = null)
    {
        if (sections.Count == 0) return PlacementTestErrors.NoSection;
        var picked = new List<Guid>();
        var missing = new List<string>();
        foreach (var s in sections)
        {
            var pool = approvedPool(s.Level, s.Skill).Where(id => !picked.Contains(id)).ToList();
            if (pool.Count < s.Count)
            {
                missing.Add($"{s.Level} · {SkillLabel(s.Skill)}: cần {s.Count}, chỉ có {pool.Count} câu đã duyệt");
                continue;
            }
            if (random is not null) random.Shuffle(System.Runtime.InteropServices.CollectionsMarshal.AsSpan(pool));
            picked.AddRange(pool.Take(s.Count));
        }
        return missing.Count > 0 ? PlacementTestErrors.NotEnoughQuestions(missing) : picked;
    }

    public static string SkillLabel(JapaneseSkill skill) => skill switch
    {
        JapaneseSkill.Vocab => "Từ vựng",
        JapaneseSkill.Kanji => "Chữ Hán",
        JapaneseSkill.Grammar => "Ngữ pháp",
        JapaneseSkill.Reading => "Đọc hiểu",
        _ => "Nghe hiểu",
    };
}
