using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Exams.Common;
using JPCenter.Modules.Academic.Application.Exams.GetExamResults;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Exams;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.GetMyExams;

/// <summary>
/// 1 kỳ thi ở cổng HV / PH: lịch thi, trạng thái làm bài, kết quả (CHỈ khi đã công bố — ND-06), đạt / chưa đạt.
/// <c>CanStart</c>: HV (không phải PH) đang học lớp, thi online đang mở, chưa nộp.
/// </summary>
public sealed record MyExamDto(ClassExamDto Exam, string ClassCode, string ClassName, AttemptState Attempt, Guid? AttemptId, bool CanStart,
    ExamResultDto? Result, bool? Passed);

/// <summary>ND-05 / ND-11 — Lịch thi + kết quả đã công bố của HV (PH chọn con). Kỳ thi nháp / đã hủy không hiện.</summary>
public sealed record GetMyExamsQuery(Guid? StudentId) : IQuery<IReadOnlyList<MyExamDto>>;

public sealed class GetMyExamsHandler(IAcademicDbContext db, LearningAccess access, IClock clock)
    : IQueryHandler<GetMyExamsQuery, IReadOnlyList<MyExamDto>>
{
    public async Task<Result<IReadOnlyList<MyExamDto>>> Handle(GetMyExamsQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(q.StudentId, ct);
        if (learner.IsFailure) return learner.Error;
        var me = learner.Value;
        var classIds = me.ClassIds.Select(c => new ClassId(c)).ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, ct);
        var exams = await db.ClassExams.AsNoTracking().Where(e => classIds.Contains(e.ClassId) && e.Status == ClassExamStatus.Published)
            .OrderByDescending(e => e.StartsAt).ToListAsync(ct);
        var examIds = exams.Select(e => e.Id).ToList();
        var attempts = await db.ExamAttempts.AsNoTracking().Where(a => a.StudentId == me.StudentId && examIds.Contains(a.ClassExamId))
            .ToDictionaryAsync(a => a.ClassExamId, ct);
        var results = await db.ExamResults.AsNoTracking().Where(r => r.StudentId == me.StudentId && classIds.Contains(r.ClassId)).ToListAsync(ct);

        var now = clock.UtcNow;
        var list = new List<MyExamDto>();
        foreach (var e in exams)
        {
            var cls = classes[e.ClassId];
            var dto = ExamReader.ToDto(e, null, 0, 0, includeQuestions: false);
            var attempt = attempts.GetValueOrDefault(e.Id);
            var state = attempt is null ? AttemptState.None : attempt.IsSubmitted ? AttemptState.Submitted : AttemptState.InProgress;
            var canStart = !access.IsParent && me.Studies(e.ClassId.Value) && e.CheckOpen(now).IsSuccess && attempt is not { IsSubmitted: true };
            var result = e.ResultsPublished ? results.FirstOrDefault(r => r.ExamId == e.ExamId && r.ClassId == e.ClassId) : null;
            bool? passed = result?.Percent is { } pct ? pct >= e.PassPercent : result is null ? null : false;
            list.Add(new MyExamDto(dto, cls.Code, cls.Name, state, attempt?.Id.Value, canStart, result is null ? null : ExamReader.ToDto(result), passed));
        }
        return list;
    }
}
