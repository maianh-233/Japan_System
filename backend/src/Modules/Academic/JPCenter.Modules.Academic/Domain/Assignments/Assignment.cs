using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Assignments;

/// <summary>
/// Bài tập giao cho lớp: Upload (HV nộp file, GV chấm) hoặc Quiz (bốc câu từ ngân hàng theo tiêu chí, tự chấm).
/// Bảng: academic.assignments
/// </summary>
public sealed class Assignment : AggregateRoot<AssignmentId>, IAuditable, ISoftDeletable
{
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
    /// <summary>GV tạo (Hr.Employee)</summary>
    public Guid CreatedByTeacherId { get; private set; }

    public static Result<Assignment> Create(ClassId classId, AssignmentType type, string title, LessonId? lessonId, string? description,
        DateTimeOffset assignedAt, DateTimeOffset dueDate, decimal maxScore, QuizConfig? quiz, List<AttachedFile> attachments, Guid teacherId)
    {
        if (dueDate <= assignedAt) return Error.Validation("academic.assignment.invalid_due", "Hạn nộp phải sau lúc giao");
        if (maxScore <= 0) return Error.Validation("academic.assignment.invalid_max_score", "Thang điểm phải lớn hơn 0");
        if (type == AssignmentType.Quiz && quiz is null) return Error.Validation("academic.assignment.quiz_required", "Bài trắc nghiệm cần tiêu chí bốc câu");
        if (quiz is { QuestionCount: <= 0 }) return Error.Validation("academic.assignment.quiz_count", "Số câu phải lớn hơn 0");
        return new Assignment
        {
            Id = AssignmentId.New(), ClassId = classId, Type = type, Title = title, LessonId = lessonId, Description = description,
            AssignedAt = assignedAt, DueDate = dueDate, MaxScore = maxScore, Quiz = type == AssignmentType.Quiz ? quiz : null,
            Attachments = type == AssignmentType.Upload ? attachments : [], CreatedByTeacherId = teacherId,
        };
    }

    public void ExtendDue(DateTimeOffset dueDate) => DueDate = dueDate;
}
