using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>
/// Bài tập giao cho lớp (ND-01): Upload (HV nộp file / chữ, GV chấm) hoặc Quiz (bốc câu ĐÃ DUYỆT từ ngân hàng theo tiêu chí, tự chấm).
/// Bảng: academic.assignments
/// </summary>
public sealed class Assignment : AggregateRoot<AssignmentId>, IAuditable, ISoftDeletable
{
    public const decimal MaxScoreLimit = 100;
    public const int MaxQuestions = 100;
    public const int MaxAttempts = 10;
    public const int MaxTimeLimitMinutes = 240;

    private Assignment() { }

    public ClassId ClassId { get; private set; }
    public AssignmentType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public LessonId? LessonId { get; private set; }
    public string? Description { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset DueDate { get; private set; }
    /// <summary>Thang điểm (mặc định 10)</summary>
    public decimal MaxScore { get; private set; }
    /// <summary>Tiêu chí bốc câu (chỉ Quiz, jsonb)</summary>
    public QuizConfig? Quiz { get; private set; }
    /// <summary>Đề bài / tài liệu GV gửi (chỉ Upload, jsonb)</summary>
    public List<AttachedFile> Attachments { get; private set; } = [];
    /// <summary>Người giao (Hr.Employee — GV hoặc giáo vụ)</summary>
    public Guid CreatedByTeacherId { get; private set; }

    public static Result<Assignment> Create(ClassId classId, AssignmentType type, string? title, LessonId? lessonId, string? description,
        DateTimeOffset assignedAt, DateTimeOffset dueDate, decimal maxScore, QuizConfig? quiz, List<AttachedFile> attachments, Guid teacherId)
    {
        var a = new Assignment { Id = AssignmentId.New(), ClassId = classId, AssignedAt = assignedAt, CreatedByTeacherId = teacherId };
        var set = a.Configure(type, lessonId, maxScore, quiz, attachments);
        if (set.IsFailure) return set.Error;
        var info = a.Edit(title, description, dueDate);
        return info.IsFailure ? info.Error : a;
    }

    /// <summary>Tiêu đề, mô tả, hạn nộp (gia hạn) — sửa được cả khi đã có bài nộp (ND-01.E6)</summary>
    public Result Edit(string? title, string? description, DateTimeOffset dueDate)
    {
        if (string.IsNullOrWhiteSpace(title)) return AssignmentErrors.TitleRequired;
        if (dueDate <= AssignedAt) return AssignmentErrors.InvalidDue;
        (Title, Description, DueDate) = (title.Trim(), Clean(description), dueDate);
        return Result.Success();
    }

    /// <summary>Loại, bài học, thang điểm, tiêu chí quiz, đề đính kèm — chỉ khi chưa có HV nộp (ND-01.E6)</summary>
    public Result Reconfigure(AssignmentType type, LessonId? lessonId, decimal maxScore, QuizConfig? quiz, List<AttachedFile> attachments,
        bool hasSubmissions)
    {
        if (hasSubmissions && (type != Type || lessonId != LessonId || maxScore != MaxScore || !Same(quiz, Quiz)))
            return AssignmentErrors.HasSubmissions;
        return Configure(type, lessonId, maxScore, quiz, attachments);
    }

    /// <summary>Nộp sau hạn → nộp muộn (ND-02.E2)</summary>
    public bool IsLate(DateTimeOffset submittedAt) => submittedAt > DueDate;

    private Result Configure(AssignmentType type, LessonId? lessonId, decimal maxScore, QuizConfig? quiz, List<AttachedFile> attachments)
    {
        if (maxScore is <= 0 or > MaxScoreLimit) return AssignmentErrors.InvalidMaxScore;
        if (type == AssignmentType.Quiz && !Valid(quiz)) return AssignmentErrors.QuizConfig;
        (Type, LessonId, MaxScore) = (type, lessonId, maxScore);
        Quiz = type == AssignmentType.Quiz ? quiz! with { LessonIds = quiz!.LessonIds.Distinct().ToList(), Skills = quiz.Skills.Distinct().ToList() } : null;
        Attachments = type == AssignmentType.Upload ? attachments : [];
        return Result.Success();
    }

    private static bool Valid(QuizConfig? q) =>
        q is { LessonIds.Count: > 0, Skills.Count: > 0, QuestionCount: > 0 and <= MaxQuestions, Attempts: >= 1 and <= MaxAttempts }
        && q.TimeLimitMinutes is null or (>= 1 and <= MaxTimeLimitMinutes);

    private static bool Same(QuizConfig? a, QuizConfig? b) =>
        a is null || b is null
            ? a is null && b is null
            : a.LessonIds.ToHashSet().SetEquals(b.LessonIds) && a.Skills.ToHashSet().SetEquals(b.Skills) && a.Difficulty == b.Difficulty
              && a.QuestionCount == b.QuestionCount && a.TimeLimitMinutes == b.TimeLimitMinutes && a.Attempts == b.Attempts && a.Shuffle == b.Shuffle;

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
