using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>Đề HV đang làm: câu hỏi (không đáp án) + câu trả lời đã lưu theo VỊ TRÍ HIỂN THỊ</summary>
/// <param name="Sections">Tên phần thi theo thứ tự</param>
public sealed record AttemptPaperDto(Guid AttemptId, Guid ExamId, string Title, DateTimeOffset StartedAt, DateTimeOffset DeadlineAt,
    IReadOnlyList<string> Sections, IReadOnlyList<PaperQuestionDto> Questions, IReadOnlyList<SavedAnswerDto> Answers);

/// <param name="Choice">Vị trí đáp án hiển thị (0-based)</param>
public sealed record SavedAnswerDto(Guid QuestionId, int Choice);

/// <summary>Bài làm thi online của HV đang gọi (cổng HV — chỉ HV, PH không làm bài) + dựng đề</summary>
public sealed class MyAttempt(IAcademicDbContext db, LearningAccess access, PaperBuilder paper)
{
    public static readonly Error NotFound = Error.NotFound("academic.exam_attempt.not_found", "Không tìm thấy bài làm");

    /// <summary>Bài làm (theo dõi để sửa) + kỳ thi, nếu là của HV đang gọi</summary>
    public async Task<Result<(ExamAttempt Attempt, ClassExam Exam)>> FindAsync(Guid attemptId, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var attempt = await db.ExamAttempts.FirstOrDefaultAsync(a => a.Id == new ExamAttemptId(attemptId), ct);
        if (attempt is null || attempt.StudentId != learner.Value.StudentId) return NotFound;
        var exam = await db.ClassExams.FirstAsync(e => e.Id == attempt.ClassExamId, ct);
        return (attempt, exam);
    }

    public async Task<AttemptPaperDto> PaperAsync(ExamAttempt a, ClassExam exam, CancellationToken ct)
    {
        var slots = a.Questions.Select(q => new PaperSlot(q.QuestionId.Value, q.Section, q.OptionOrder)).ToList();
        var order = a.Questions.ToDictionary(q => q.QuestionId, q => q.OptionOrder);
        var answers = a.Answers.Where(x => order.ContainsKey(x.QuestionId))
            .Select(x => new SavedAnswerDto(x.QuestionId.Value, order[x.QuestionId].IndexOf(x.Choice))).ToList();
        return new AttemptPaperDto(a.Id.Value, exam.Id.Value, exam.Title, a.StartedAt, a.DeadlineAt, exam.Sections.Select(s => s.Name).ToList(),
            await paper.PaperAsync(slots, ct), answers);
    }
}
