namespace JPCenter.Modules.Academic.Application.Questions.Common;

/// <summary>
/// 1 câu hỏi cho trang Ngân hàng câu hỏi (nhân viên) — khớp <c>Question</c> ở <c>frontend/src/features/academic/questions/types.ts</c>,
/// thêm Id người soạn / duyệt. Có đáp án, giải thích, lời thoại → KHÔNG dùng cho cổng HV.
/// </summary>
/// <param name="Level">"N5" … "N1"</param>
/// <param name="Author">Tên người soạn</param>
/// <param name="RowVersion">Chống sửa đè khi lưu</param>
public sealed record QuestionDto(
    Guid Id,
    string Code,
    string Level,
    string Skill,
    string Format,
    string Difficulty,
    string Stem,
    string? Passage,
    QuestionAudioDto? Audio,
    string? Transcript,
    IReadOnlyList<string> Options,
    int Answer,
    string? Explanation,
    Guid? CurriculumId,
    IReadOnlyList<Guid> LessonIds,
    IReadOnlyList<string> Tags,
    string Status,
    Guid AuthorId,
    string Author,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    QuestionReviewDto? Review,
    QuestionStatsDto Stats,
    uint RowVersion);

/// <param name="Name">Tên file gốc</param>
public sealed record QuestionAudioDto(string Name, int DurationSec);

/// <param name="By">Tên người duyệt / trả lại / ngừng dùng</param>
public sealed record QuestionReviewDto(Guid ById, string By, DateTimeOffset At, string? Note);

/// <param name="Picks">Số lần chọn từng đáp án — null khi chưa có lượt làm</param>
public sealed record QuestionStatsDto(int Used, int Attempts, int Correct, IReadOnlyList<int>? Picks);
