using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>
/// Nộp bài thi online (HV bấm nộp / hệ thống tự nộp khi hết giờ — ND-05.E4): chấm từng phần theo đáp án trong ngân hàng, ghi kết quả
/// (<c>exam_results</c>, 1 / HV × mốc thi) + thống kê lượt làm của câu hỏi. Gọi xong thì <c>SaveChangesAsync</c>.
/// </summary>
public sealed class ExamGrader(IAcademicDbContext db)
{
    public async Task<Result<ExamResult>> SubmitAsync(ExamAttempt attempt, ClassExam exam, DateTimeOffset now, CancellationToken ct)
    {
        var ids = attempt.Questions.Select(q => q.QuestionId).ToList();
        var questions = await db.Questions.IgnoreQueryFilters().Where(q => ids.Contains(q.Id)).ToDictionaryAsync(q => q.Id, ct);
        var submitted = attempt.Submit(exam, questions.ToDictionary(q => q.Key, q => q.Value.Answer), now);
        if (submitted.IsFailure) return submitted.Error;

        var chosen = attempt.Answers.ToDictionary(a => a.QuestionId, a => a.Choice);
        foreach (var (id, q) in questions) q.RecordAnswer(chosen.TryGetValue(id, out var c) ? c : -1);

        var result = await db.ExamResults.FirstOrDefaultAsync(r => r.ClassId == exam.ClassId && r.ExamId == exam.ExamId
                                                                    && r.StudentId == attempt.StudentId, ct);
        if (result is null)
        {
            var created = ExamResult.Record(exam.ClassId, exam.ExamId, exam.Id, attempt.StudentId, submitted.Value, attempt.StartedAt,
                attempt.DurationMinutes, null);
            if (created.IsFailure) return created.Error;
            db.ExamResults.Add(created.Value);
            return created.Value;
        }
        var updated = result.UpdateScores(submitted.Value, result.Comment);
        return updated.IsFailure ? updated.Error : result;
    }
}
