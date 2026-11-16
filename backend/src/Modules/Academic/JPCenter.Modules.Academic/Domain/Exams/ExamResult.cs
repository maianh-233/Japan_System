using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;

namespace JPCenter.Modules.Academic.Domain.Exams;

/// <summary>Kết quả 1 HV trong 1 kỳ thi (mốc thi của lộ trình) của lớp. Bảng: academic.exam_results, UQ (class_id, exam_id, student_id)</summary>
public sealed class ExamResult : AggregateRoot<ExamResultId>, IAuditable
{
    private ExamResult() { }

    public ClassId ClassId { get; private set; }
    public ExamMilestoneId ExamId { get; private set; }
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

    public static ExamResult Record(ClassId classId, ExamMilestoneId examId, Guid studentId, List<ExamSectionScore> sections,
        DateTimeOffset? startedAt, int? durationMinutes, string? comment) =>
        new()
        {
            Id = ExamResultId.New(), ClassId = classId, ExamId = examId, StudentId = studentId, Status = ExamAttendance.Done, Sections = sections,
            TotalScore = sections.Sum(s => s.Score), MaxScore = sections.Sum(s => s.Max), StartedAt = startedAt, DurationMinutes = durationMinutes,
            Comment = comment,
        };

    public static ExamResult Absent(ClassId classId, ExamMilestoneId examId, Guid studentId) =>
        new() { Id = ExamResultId.New(), ClassId = classId, ExamId = examId, StudentId = studentId, Status = ExamAttendance.Absent };

    public void UpdateComment(string? comment) => Comment = comment;
}
