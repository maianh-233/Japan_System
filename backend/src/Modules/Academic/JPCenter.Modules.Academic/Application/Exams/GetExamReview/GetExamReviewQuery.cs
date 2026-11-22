using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.GetExamReview;

/// <summary>Xem lại bài thi online sau khi công bố kết quả: từng câu, lựa chọn của HV, đáp án, giải thích</summary>
public sealed record ExamReviewDto(Guid ExamId, string Title, IReadOnlyList<string> Sections, IReadOnlyList<ReviewQuestionDto> Questions);

/// <summary>ND-06 — đáp án thi online chỉ xem được khi giáo vụ đã công bố kết quả (quy tắc vàng 2). HV hoặc PH (xem bài của con).</summary>
public sealed record GetExamReviewQuery(Guid ExamId, Guid? StudentId) : IQuery<ExamReviewDto>;

public sealed class GetExamReviewHandler(IAcademicDbContext db, LearningAccess access, PaperBuilder paper) : IQueryHandler<GetExamReviewQuery, ExamReviewDto>
{
    public static readonly Error NotPublished = Error.Conflict("academic.exam.results_not_published", "Kết quả chưa được công bố");

    public async Task<Result<ExamReviewDto>> Handle(GetExamReviewQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(q.StudentId, ct);
        if (learner.IsFailure) return learner.Error;
        var exam = await db.ClassExams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == new ClassExamId(q.ExamId), ct);
        if (exam is null || !learner.Value.Enrolled(exam.ClassId.Value) || exam.Status != ClassExamStatus.Published) return ExamErrors.NotFound;
        if (!exam.ResultsPublished) return NotPublished;
        var attempt = await db.ExamAttempts.AsNoTracking()
            .FirstOrDefaultAsync(a => a.ClassExamId == exam.Id && a.StudentId == learner.Value.StudentId && a.SubmittedAt != null, ct);
        if (attempt is null) return MyAttempt.NotFound;

        var slots = attempt.Questions.Select(x => new PaperSlot(x.QuestionId.Value, x.Section, x.OptionOrder)).ToList();
        var chosen = attempt.Answers.ToDictionary(a => a.QuestionId.Value, a => a.Choice);
        return new ExamReviewDto(exam.Id.Value, exam.Title, exam.Sections.Select(s => s.Name).ToList(),
            await paper.GradeAsync(slots, chosen, recordStats: false, ct));
    }
}
