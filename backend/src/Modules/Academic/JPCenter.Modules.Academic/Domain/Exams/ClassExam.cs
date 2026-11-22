using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.Modules.Academic.Domain.Questions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>
/// Kỳ thi của lớp theo 1 MỐC THI của lộ trình (ND-04): online (HV làm trên Cổng trong khung giờ, tự chấm) hoặc giấy (in đề, nhập điểm).
/// Đề = các phần, câu bốc từ Ngân hàng (đã duyệt, trong phạm vi bài của mốc — Application kiểm). Nháp đổi được đề; phát hành → đề chốt.
/// Công bố kết quả → HV / PH thấy điểm (+ đáp án với thi online). Bảng: academic.class_exams, UQ (class_id, exam_id) khi chưa hủy
/// </summary>
public sealed class ClassExam : AggregateRoot<ClassExamId>, IAuditable
{
    public const int MinDuration = 10;
    public const int MaxDuration = 240;
    public const decimal DefaultPassPercent = 60;
    public const decimal MaxPoints = 100;

    private ClassExam() { }

    public ClassId ClassId { get; private set; }
    /// <summary>Mốc thi của lộ trình (curriculum exam milestone)</summary>
    public ExamMilestoneId ExamId { get; private set; }
    public string Title { get; private set; } = null!;
    public ExamMode Mode { get; private set; }
    public DateTimeOffset StartsAt { get; private set; }
    public int DurationMinutes { get; private set; }
    /// <summary>% điểm tối thiểu để đạt</summary>
    public decimal PassPercent { get; private set; }
    /// <summary>Online: mỗi HV 1 đề (đảo câu trong phần + đảo đáp án)</summary>
    public bool Shuffle { get; private set; }
    /// <summary>Các phần thi + câu đã bốc (jsonb)</summary>
    public List<ExamSection> Sections { get; private set; } = [];
    public ClassExamStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    /// <summary>Lúc công bố kết quả — null = HV / PH chưa thấy điểm</summary>
    public DateTimeOffset? ResultsPublishedAt { get; private set; }
    public DateTimeOffset EndsAt => StartsAt.AddMinutes(DurationMinutes);
    public decimal MaxScore => Sections.Sum(s => s.Max);
    public int QuestionCount => Sections.Sum(s => s.QuestionIds.Count);
    public bool ResultsPublished => ResultsPublishedAt is not null;

    public static Result<ClassExam> Create(ClassId classId, ExamMilestoneId examId, string? title, ExamMode mode, DateTimeOffset startsAt,
        int durationMinutes, decimal passPercent, bool shuffle, List<ExamSection> sections)
    {
        var exam = new ClassExam { Id = ClassExamId.New(), ClassId = classId, ExamId = examId, Status = ClassExamStatus.Draft };
        var set = exam.Set(title, mode, startsAt, durationMinutes, passPercent, shuffle, sections);
        return set.IsFailure ? set.Error : exam;
    }

    /// <summary>Sửa kỳ thi nháp (đổi đề, giờ, hình thức...) — đã phát hành → E4</summary>
    public Result UpdateDraft(string? title, ExamMode mode, DateTimeOffset startsAt, int durationMinutes, decimal passPercent, bool shuffle,
        List<ExamSection> sections)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (Status == ClassExamStatus.Published) return ExamErrors.Published;
        return Set(title, mode, startsAt, durationMinutes, passPercent, shuffle, sections);
    }

    /// <summary>ND-04.E4 — đề đã chốt chỉ đổi giờ thi, khi chưa ai làm bài</summary>
    public Result Reschedule(DateTimeOffset startsAt, int durationMinutes, bool hasAttempts)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (hasAttempts) return ExamErrors.HasAttempts;
        if (durationMinutes is < MinDuration or > MaxDuration) return ExamErrors.InvalidDuration;
        (StartsAt, DurationMinutes) = (startsAt, durationMinutes);
        return Result.Success();
    }

    /// <summary>Phát hành: đề chốt; online → HV thấy ở Lịch thi</summary>
    public Result Publish(DateTimeOffset at)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (Status == ClassExamStatus.Published) return ExamErrors.Published;
        (Status, PublishedAt) = (ClassExamStatus.Published, at);
        return Result.Success();
    }

    /// <summary>Hủy kỳ thi (làm lại kỳ thi khác cho mốc này) — đã có bài làm / kết quả → E5</summary>
    public Result Cancel(bool hasAttempts)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (hasAttempts) return ExamErrors.HasAttempts;
        Status = ClassExamStatus.Cancelled;
        return Result.Success();
    }

    /// <summary>ND-06 — công bố kết quả (thi online: sau khi hết giờ thi). Công bố lại sau khi sửa điểm: giữ mốc lần đầu.</summary>
    public Result PublishResults(DateTimeOffset at)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (Status != ClassExamStatus.Published) return ExamErrors.NotPublished;
        if (Mode == ExamMode.Online && at < EndsAt) return ExamErrors.NotFinished;
        ResultsPublishedAt ??= at;
        return Result.Success();
    }

    /// <summary>ND-05.E1 — HV bắt đầu / tiếp tục làm được: online, đã phát hành, trong khung giờ thi</summary>
    public Result CheckOpen(DateTimeOffset now)
    {
        if (Status == ClassExamStatus.Cancelled) return ExamErrors.Cancelled;
        if (Mode == ExamMode.Paper) return ExamErrors.PaperExam;
        if (Status != ClassExamStatus.Published || now < StartsAt || now >= EndsAt) return ExamErrors.NotOpen;
        return Result.Success();
    }

    /// <summary>Phần thi chứa câu hỏi (-1 nếu không có)</summary>
    public int SectionOf(QuestionId questionId) => Sections.FindIndex(s => s.QuestionIds.Contains(questionId));

    private Result Set(string? title, ExamMode mode, DateTimeOffset startsAt, int durationMinutes, decimal passPercent, bool shuffle,
        List<ExamSection> sections)
    {
        if (string.IsNullOrWhiteSpace(title)) return ExamErrors.TitleRequired;
        if (durationMinutes is < MinDuration or > MaxDuration) return ExamErrors.InvalidDuration;
        if (passPercent is <= 0 or > 100) return ExamErrors.InvalidPassPercent;
        var all = sections.SelectMany(s => s.QuestionIds).ToList();
        if (sections.Count == 0 || sections.Any(s => string.IsNullOrWhiteSpace(s.Name) || s.QuestionIds.Count == 0 || s.Points is <= 0 or > MaxPoints)
            || all.Distinct().Count() != all.Count)
            return ExamErrors.Sections;
        (Title, Mode, StartsAt, DurationMinutes, PassPercent, Shuffle) = (title.Trim(), mode, startsAt, durationMinutes, passPercent, shuffle);
        Sections = sections.Select(s => s with { Name = s.Name.Trim() }).ToList();
        return Result.Success();
    }
}
