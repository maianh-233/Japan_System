using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Domain.Jlpt;

/// <summary>
/// HV-16 bước 2 — quy chế chấm JLPT (giống frontend <c>features/student/jlpt/jlpt.ts</c>): N1 → N3 có 3 phần × 60 (liệt 19);
/// N4, N5 có 2 phần: Kiến thức ngôn ngữ · Đọc hiểu 120 (liệt 38) + Nghe 60 (liệt 19). Đỗ = tổng ≥ điểm chuẩn và không phần nào liệt.
/// </summary>
public static class JlptScoring
{
    private static readonly JlptSection[] ThreeParts =
        [new("Kiến thức ngôn ngữ", 60, 19), new("Đọc hiểu", 60, 19), new("Nghe hiểu", 60, 19)];
    private static readonly JlptSection[] TwoParts =
        [new("Kiến thức ngôn ngữ · Đọc hiểu", 120, 38), new("Nghe hiểu", 60, 19)];

    /// <summary>Cấp độ thi được (N5 → N1, không có Beginner)</summary>
    public static bool IsExamLevel(JlptLevel level) => level is >= JlptLevel.N5 and <= JlptLevel.N1;

    public static IReadOnlyList<JlptSection> Sections(JlptLevel level) => level is JlptLevel.N4 or JlptLevel.N5 ? TwoParts : ThreeParts;

    /// <summary>Điểm tổng tối thiểu để đỗ (thang 180)</summary>
    public static int PassTotal(JlptLevel level) => level switch
    {
        JlptLevel.N1 => 100,
        JlptLevel.N2 => 90,
        JlptLevel.N3 => 95,
        JlptLevel.N4 => 90,
        _ => 80,
    };

    /// <summary>Chấm; null nếu cấp độ không thi được hoặc số phần / điểm không khớp cấu trúc đề</summary>
    public static JlptResult? Judge(JlptLevel level, IReadOnlyList<int> scores)
    {
        if (!IsExamLevel(level)) return null;
        var sections = Sections(level);
        if (scores.Count != sections.Count || scores.Where((s, i) => s < 0 || s > sections[i].Max).Any()) return null;
        var fails = sections.Where((s, i) => scores[i] < s.Min).Select(s => s.Name).ToList();
        var total = scores.Sum();
        return new JlptResult(total, total >= PassTotal(level) && fails.Count == 0, fails);
    }
}
