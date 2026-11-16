using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Questions;

/// <summary>
/// Câu hỏi trong Ngân hàng câu hỏi — nguồn DUY NHẤT cho quiz, kỳ thi, test xếp lớp. Chỉ câu Approved mới được bốc vào đề.
/// Nội dung hỗ trợ furigana 漢字{かんじ}. Bảng: academic.questions
/// </summary>
public sealed class Question : AggregateRoot<QuestionId>, IAuditable, ISoftDeletable
{
    private Question() { }

    public string Code { get; private set; } = null!;
    public JlptLevel Level { get; private set; }
    public JapaneseSkill Skill { get; private set; }
    public QuestionFormat Format { get; private set; }
    public QuestionDifficulty Difficulty { get; private set; }
    /// <summary>Đề bài (furigana, chỗ trống ＿＿, vị trí ★)</summary>
    public string Stem { get; private set; } = null!;
    /// <summary>Đoạn văn đọc hiểu (nhiều câu dùng chung)</summary>
    public string? Passage { get; private set; }
    /// <summary>File nghe (khóa trong kho file)</summary>
    public string? AudioKey { get; private set; }
    public int? AudioDurationSec { get; private set; }
    /// <summary>Lời thoại — chỉ GV / nhân viên xem</summary>
    public string? Transcript { get; private set; }
    public List<string> Options { get; private set; } = [];
    /// <summary>Vị trí đáp án đúng (0-based) — không trả về cổng HV trước khi nộp</summary>
    public int Answer { get; private set; }
    public string? Explanation { get; private set; }
    public CurriculumId? CurriculumId { get; private set; }
    /// <summary>Gắn bài học để bốc câu theo bài</summary>
    public List<LessonId> LessonIds { get; private set; } = [];
    public List<string> Tags { get; private set; } = [];
    public QuestionStatus Status { get; private set; }
    public Guid AuthorId { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    /// <summary>Lý do trả lại / ghi chú duyệt</summary>
    public string? ReviewNote { get; private set; }
    /// <summary>Thống kê: số đề đã dùng, lượt làm, lượt đúng, số lần chọn từng đáp án</summary>
    public int UsedCount { get; private set; }
    public int AttemptCount { get; private set; }
    public int CorrectCount { get; private set; }
    public List<int> Picks { get; private set; } = [];

    public static Result<Question> Create(string code, JlptLevel level, JapaneseSkill skill, QuestionFormat format, QuestionDifficulty difficulty,
        string stem, string? passage, List<string> options, int answer, string? explanation, CurriculumId? curriculumId,
        List<LessonId> lessonIds, List<string> tags, Guid authorId)
    {
        if (level == JlptLevel.Beginner) return Error.Validation("academic.question.invalid_level", "Câu hỏi phải gắn cấp độ N5–N1");
        if (string.IsNullOrWhiteSpace(stem)) return Error.Validation("academic.question.stem_required", "Chưa nhập đề bài");
        if (options.Count < 2) return Error.Validation("academic.question.options", "Cần ít nhất 2 đáp án");
        if (answer < 0 || answer >= options.Count) return Error.Validation("academic.question.answer", "Đáp án đúng không hợp lệ");
        return new Question
        {
            Id = QuestionId.New(), Code = code, Level = level, Skill = skill, Format = format, Difficulty = difficulty, Stem = stem,
            Passage = passage, Options = options, Answer = answer, Explanation = explanation, CurriculumId = curriculumId,
            LessonIds = lessonIds, Tags = tags, AuthorId = authorId, Status = QuestionStatus.Draft, Picks = [.. new int[options.Count]],
        };
    }

    public void SetAudio(string audioKey, int durationSec, string? transcript) => (AudioKey, AudioDurationSec, Transcript) = (audioKey, durationSec, transcript);

    public Result SubmitForReview()
    {
        if (Status != QuestionStatus.Draft) return QuestionErrors.InvalidStatus;
        Status = QuestionStatus.Review;
        return Result.Success();
    }

    public Result Approve(Guid by, DateTimeOffset at) => Review(QuestionStatus.Approved, by, at, null);

    /// <summary>Trả lại cho người soạn sửa</summary>
    public Result Return(Guid by, DateTimeOffset at, string note) => Review(QuestionStatus.Draft, by, at, note);

    public void Retire(string? note) => (Status, ReviewNote) = (QuestionStatus.Retired, note ?? ReviewNote);

    public void RecordUse() => UsedCount++;

    public void RecordAnswer(int picked)
    {
        AttemptCount++;
        if (picked == Answer) CorrectCount++;
        if (picked >= 0 && picked < Picks.Count) Picks[picked]++;
    }

    private Result Review(QuestionStatus to, Guid by, DateTimeOffset at, string? note)
    {
        if (Status != QuestionStatus.Review) return QuestionErrors.InvalidStatus;
        if (by == AuthorId) return Error.Forbidden("academic.question.self_review", "Người soạn không tự duyệt câu hỏi của mình");
        (Status, ReviewedBy, ReviewedAt, ReviewNote) = (to, by, at, note);
        return Result.Success();
    }
}
