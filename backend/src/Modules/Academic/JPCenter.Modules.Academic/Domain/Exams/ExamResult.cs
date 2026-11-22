using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>
/// Kết quả 1 HV trong 1 kỳ thi (mốc thi của lộ trình) của lớp — ND-06: thi online tự chấm từ bài làm, thi giấy nhập điểm từng phần, vắng thi.
/// Luôn sửa được (sửa sau công bố → nhật ký). Bảng: academic.exam_results, UQ (class_id, exam_id, student_id)
/// </summary>
public sealed class ExamResult : AggregateRoot<ExamResultId>, IAuditable
{
    private ExamResult() { }

    public ClassId ClassId { get; private set; }
    /// <summary>Mốc thi của lộ trình</summary>
    public ExamMilestoneId ExamId { get; private set; }
    /// <summary>Kỳ thi sinh ra kết quả (null = dữ liệu cũ nhập trước khi có kỳ thi)</summary>
    public ClassExamId? ClassExamId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    public ExamAttendance Status { get; private set; }
    /// <summary>Điểm từng phần (jsonb) — tổng = điểm bài thi</summary>
    public List<ExamSectionScore> Sections { get; private set; } = [];
    public decimal TotalScore { get; private set; }
    public decimal MaxScore { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? Comment { get; private set; }

    /// <summary>% điểm (vắng thi → null)</summary>
    public decimal? Percent => Status == ExamAttendance.Done && MaxScore > 0 ? decimal.Round(TotalScore * 100 / MaxScore, 1) : null;

    public static Result<ExamResult> Record(ClassId classId, ExamMilestoneId examId, ClassExamId? classExamId, Guid studentId,
        List<ExamSectionScore> sections, DateTimeOffset? startedAt, int? durationMinutes, string? comment)
    {
        var r = new ExamResult { Id = ExamResultId.New(), ClassId = classId, ExamId = examId, ClassExamId = classExamId, StudentId = studentId };
        var set = r.UpdateScores(sections, comment);
        if (set.IsFailure) return set.Error;
        (r.StartedAt, r.DurationMinutes) = (startedAt, durationMinutes);
        return r;
    }

    public static ExamResult Absent(ClassId classId, ExamMilestoneId examId, ClassExamId? classExamId, Guid studentId) =>
        new()
        {
            Id = ExamResultId.New(), ClassId = classId, ExamId = examId, ClassExamId = classExamId, StudentId = studentId,
            Status = ExamAttendance.Absent,
        };

    /// <summary>Nhập / sửa điểm từng phần (0 → tối đa phần — E1) + nhận xét; HV vắng mà có điểm → chuyển "đã thi"</summary>
    public Result UpdateScores(List<ExamSectionScore> sections, string? comment)
    {
        if (sections.Count == 0 || sections.Any(s => s.Max <= 0 || s.Score < 0 || s.Score > s.Max || string.IsNullOrWhiteSpace(s.Name)))
            return ExamErrors.InvalidScore;
        (Status, Sections, TotalScore, MaxScore) = (ExamAttendance.Done, sections, sections.Sum(s => s.Score), sections.Sum(s => s.Max));
        Comment = Clean(comment);
        return Result.Success();
    }

    /// <summary>ND-06.E3 — vắng thi: bỏ điểm (giữ nhận xét)</summary>
    public void MarkAbsent(string? comment)
    {
        (Status, Sections, TotalScore, MaxScore, StartedAt, DurationMinutes) = (ExamAttendance.Absent, [], 0, 0, null, null);
        Comment = Clean(comment);
    }

    public void UpdateComment(string? comment) => Comment = Clean(comment);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
