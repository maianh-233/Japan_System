namespace JPCenter.Modules.Academic.Application.Learning.Common;

/// <summary>
/// 1 câu trong đề phát cho HV (quiz bài tập / bài thi online) — KHÔNG có đáp án, giải thích, lời thoại (quy tắc vàng 2).
/// <c>Options</c> theo thứ tự hiển thị (đã đảo nếu bật đảo đề); HV trả lời bằng vị trí hiển thị (0-based).
/// </summary>
/// <param name="Section">Phần thi (kỳ thi) — quiz luôn 0</param>
/// <param name="AudioUrl">Link tạm file nghe</param>
public sealed record PaperQuestionDto(Guid Id, int Section, string Skill, string Format, string Stem, string? Passage, string? AudioUrl,
    int? AudioDurationSec, IReadOnlyList<string> Options);

/// <summary>Xem lại 1 câu sau khi nộp: đáp án đúng + lựa chọn của HV theo VỊ TRÍ HIỂN THỊ của đề HV đã làm</summary>
/// <param name="Chosen">null = bỏ trống</param>
public sealed record ReviewQuestionDto(Guid QuestionId, int Section, string Stem, string? Passage, IReadOnlyList<string> Options, int? Chosen,
    int Answer, bool Correct, string? Explanation);
