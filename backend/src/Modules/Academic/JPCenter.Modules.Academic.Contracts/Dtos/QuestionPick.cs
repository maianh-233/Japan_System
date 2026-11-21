namespace JPCenter.Modules.Academic.Contracts.Dtos;

/// <summary>Tiêu chí bốc câu (NH-09) — khớp <c>QuizCriteria</c> của frontend</summary>
/// <param name="Level">"N5" … "N1"</param>
/// <param name="Skills">"vocab" · "kanji" · "grammar" · "reading" · "listening" — rỗng = mọi kỹ năng</param>
/// <param name="Difficulty">"easy" · "medium" · "hard" · null = trộn</param>
/// <param name="LessonIds">Academic.Lesson — rỗng = không lọc theo bài</param>
/// <param name="Exclude">Câu không lấy (đã có trong đề / đã làm)</param>
public sealed record QuestionPickCriteria(
    string Level,
    IReadOnlyList<string> Skills,
    string? Difficulty,
    Guid? CurriculumId,
    IReadOnlyList<Guid> LessonIds,
    int Count,
    IReadOnlyCollection<Guid>? Exclude = null);

/// <param name="Missing">Thiếu bao nhiêu câu so với yêu cầu (0 = đủ)</param>
public sealed record QuestionPick(IReadOnlyList<PickedQuestion> Questions, int Missing);

/// <summary>1 câu trong đề — KHÔNG có đáp án, giải thích, lời thoại</summary>
/// <param name="AudioKey">Khóa file nghe trong kho — module dùng tự cấp link tạm</param>
public sealed record PickedQuestion(Guid Id, string Code, string Level, string Skill, string Format, string Difficulty, string Stem, string? Passage,
    string? AudioKey, int? AudioDurationSec, IReadOnlyList<string> Options);

/// <param name="Picked">Vị trí đã chọn, -1 = bỏ trống</param>
public sealed record QuestionAnswer(Guid QuestionId, int Picked);
