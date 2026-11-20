using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Questions;

/// <summary>
/// Nội dung người soạn nhập cho 1 câu hỏi (NH-02, NH-03) — dùng chung khi tạo, sửa, nhập Excel.
/// File nghe đi riêng (<see cref="Question.SetAudio"/>). Lộ trình ↔ bài học kiểm tra ở tầng Application (cần dữ liệu lộ trình).
/// </summary>
/// <param name="Stem">Đề bài — furigana 漢字{かんじ}, chỗ trống ＿＿, vị trí ★</param>
/// <param name="Passage">Đoạn văn — bắt buộc với đọc hiểu</param>
/// <param name="Transcript">Lời thoại file nghe — chỉ GV / nhân viên xem</param>
/// <param name="Options">Đúng 4 đáp án / 4 mảnh câu, không trùng nhau</param>
/// <param name="Answer">Vị trí đáp án đúng (0-based)</param>
public sealed record QuestionContent(
    JlptLevel Level,
    JapaneseSkill Skill,
    QuestionFormat Format,
    QuestionDifficulty Difficulty,
    string Stem,
    string? Passage,
    string? Transcript,
    IReadOnlyList<string> Options,
    int Answer,
    string? Explanation,
    CurriculumId? CurriculumId,
    IReadOnlyList<LessonId> LessonIds,
    IReadOnlyList<string> Tags)
{
    public const int OptionCount = 4;
    public const int StemMaxLength = 500;
    public const int PassageMaxLength = 2000;
    public const int TextMaxLength = 2000;
    public const int OptionMaxLength = 200;
    public const char Star = '★';

    /// <summary>Kiểm tra bất biến của nội dung (NH-02.E1 → E4, E7). Trả bản đã chuẩn hóa (cắt khoảng trắng, bỏ thẻ trùng / rỗng).</summary>
    public Result<QuestionContent> Normalize()
    {
        if (Level == JlptLevel.Beginner || !Enum.IsDefined(Level)) return QuestionErrors.InvalidLevel;
        if (!Enum.IsDefined(Skill) || !Enum.IsDefined(Format) || !Enum.IsDefined(Difficulty)) return QuestionErrors.InvalidCategory;

        var stem = Stem?.Trim() ?? "";
        if (stem.Length == 0) return QuestionErrors.StemRequired;
        if (stem.Length > StemMaxLength) return QuestionErrors.TooLong("Đề bài", StemMaxLength);
        if (Format == QuestionFormat.Star && !stem.Contains(Star)) return QuestionErrors.StarMissing;

        var passage = Clean(Passage);
        if (Skill == JapaneseSkill.Reading && passage is null) return QuestionErrors.PassageRequired;
        if (passage?.Length > PassageMaxLength) return QuestionErrors.TooLong("Đoạn văn", PassageMaxLength);

        var options = (Options ?? []).Select(o => o?.Trim() ?? "").ToList();
        if (options.Count != OptionCount || options.Any(o => o.Length == 0)) return QuestionErrors.Options;
        if (options.Any(o => o.Length > OptionMaxLength)) return QuestionErrors.TooLong("Đáp án", OptionMaxLength);
        if (options.Select(Comparable).Distinct().Count() != OptionCount) return QuestionErrors.Options;
        if (Answer is < 0 or >= OptionCount) return QuestionErrors.AnswerInvalid;

        var explanation = Clean(Explanation);
        var transcript = Skill == JapaneseSkill.Listening ? Clean(Transcript) : null;
        if (explanation?.Length > TextMaxLength) return QuestionErrors.TooLong("Giải thích", TextMaxLength);
        if (transcript?.Length > TextMaxLength) return QuestionErrors.TooLong("Lời thoại", TextMaxLength);

        var lessons = (LessonIds ?? []).Distinct().ToList();
        if (lessons.Count > 0 && CurriculumId is null) return QuestionErrors.LessonInvalid;

        var tags = (Tags ?? []).Select(t => t?.Trim() ?? "").Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToList();

        return this with
        {
            Stem = stem, Passage = passage, Transcript = transcript, Options = options, Explanation = explanation, LessonIds = lessons, Tags = tags,
        };
    }

    /// <summary>So sánh đáp án trùng: bỏ khoảng trắng (cả khoảng trắng tiếng Nhật), không phân biệt hoa thường</summary>
    private static string Comparable(string s) => string.Concat(s.Where(c => !char.IsWhiteSpace(c))).ToLowerInvariant();

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
