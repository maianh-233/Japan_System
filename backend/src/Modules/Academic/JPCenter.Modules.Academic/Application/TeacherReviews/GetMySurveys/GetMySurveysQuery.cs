using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Application.TeacherReviews.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.TeacherReviews.GetMySurveys;

/// <summary>ND-08 — cổng HV: khảo sát đánh giá GV của các lớp đang học (đợt đang mở, GV của lớp, đã đánh giá chưa)</summary>
public sealed record GetMySurveysQuery : IQuery<IReadOnlyList<ReviewSurveyDto>>;

public sealed class GetMySurveysHandler(IAcademicDbContext db, LearningAccess access, TeacherReviewReader reader, PeopleNames people, IClock clock)
    : IQueryHandler<GetMySurveysQuery, IReadOnlyList<ReviewSurveyDto>>
{
    public async Task<Result<IReadOnlyList<ReviewSurveyDto>>> Handle(GetMySurveysQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(null, ct);
        if (learner.IsFailure) return learner.Error;
        var me = learner.Value.StudentId;
        var ids = learner.Value.StudyingClassIds.Select(c => new ClassId(c)).ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => ids.Contains(c.Id)).ToListAsync(ct);
        var done = await db.TeacherReviews.AsNoTracking().Where(r => r.StudentId == me && ids.Contains(r.ClassId))
            .Select(r => new { r.ClassId, r.TeacherId, r.Period }).ToListAsync(ct);
        var names = await people.OfAsync(classes.SelectMany(c => c.TeacherIds).Select(t => (Guid?)t), ct);

        var result = new List<ReviewSurveyDto>();
        foreach (var c in classes.OrderBy(c => c.Code))
        {
            var period = await reader.OpenPeriodAsync(c, clock.Today, ct);
            var teachers = c.TeacherIds.Select(t => new SurveyTeacherDto(t, names.GetValueOrDefault(t, "—"),
                period is { } p && done.Any(d => d.ClassId == c.Id && d.TeacherId == t && d.Period == p))).ToList();
            result.Add(new ReviewSurveyDto(c.Id.Value, c.Code, c.Name, period, teachers));
        }
        return result;
    }
}
