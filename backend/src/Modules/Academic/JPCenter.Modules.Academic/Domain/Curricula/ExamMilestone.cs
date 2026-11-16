using JPCenter.SharedKernel.Domain;

namespace JPCenter.Modules.Academic.Domain.Curricula;

/// <summary>
/// Mốc thi giữa kỳ / cuối kỳ trong lộ trình — thi SAU lesson AfterLessonId. Phạm vi = các bài từ sau mốc trước đến mốc này.
/// Đề tạo khi tổ chức thi (lấy từ ngân hàng câu hỏi). Bảng: academic.exam_milestones
/// </summary>
public sealed class ExamMilestone : Entity<ExamMilestoneId>
{
    private ExamMilestone() { }

    internal ExamMilestone(ExamKind kind, string title, LessonId afterLessonId, string? note) : base(ExamMilestoneId.New()) =>
        (Kind, Title, AfterLessonId, Note) = (kind, title, afterLessonId, note);

    public ExamKind Kind { get; private set; }
    public string Title { get; private set; } = null!;
    public LessonId AfterLessonId { get; private set; }
    /// <summary>Ghi chú cho người tạo kỳ thi</summary>
    public string? Note { get; private set; }
}
