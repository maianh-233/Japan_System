using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.Curricula;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Exams.Common;

/// <summary>Lộ trình của lớp: trình độ + mốc thi + phạm vi bài từng mốc</summary>
public sealed record ClassCurriculum(JlptLevel Level, IReadOnlyList<ExamMilestone> Milestones, IReadOnlyDictionary<ExamMilestoneId, IReadOnlyList<LessonId>> Scopes)
{
    public ExamMilestone? Find(ExamMilestoneId id) => Milestones.FirstOrDefault(m => m.Id == id);
}

/// <summary>
/// Phạm vi bài của mốc thi (ND-04 bước 1): các bài SAU mốc thi đứng trước → tới bài của mốc (theo thứ tự bài trong lộ trình).
/// Lớp không có lộ trình → không có mốc thi.
/// </summary>
public sealed class ExamScope(IAcademicDbContext db)
{
    public static readonly Error MilestoneNotFound = Error.NotFound("academic.exam.milestone_not_found", "Mốc thi không thuộc lộ trình của lớp");

    public async Task<ClassCurriculum> OfAsync(Class cls, CancellationToken ct)
    {
        if (cls.CurriculumId is not { } cid) return new ClassCurriculum(JlptLevel.N5, [], new Dictionary<ExamMilestoneId, IReadOnlyList<LessonId>>());
        var c = await db.Curricula.AsNoTracking().AsSplitQuery().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.Id == cid, ct);
        if (c is null) return new ClassCurriculum(JlptLevel.N5, [], new Dictionary<ExamMilestoneId, IReadOnlyList<LessonId>>());

        var order = c.Lessons.ToDictionary(l => l.Id, l => l.Order);
        int OrderOf(ExamMilestone m) => order.GetValueOrDefault(m.AfterLessonId, int.MaxValue);
        var milestones = c.Exams.OrderBy(OrderOf).ToList();
        var scopes = new Dictionary<ExamMilestoneId, IReadOnlyList<LessonId>>();
        var previous = int.MinValue;
        foreach (var m in milestones)
        {
            var upTo = OrderOf(m);
            scopes[m.Id] = c.Lessons.Where(l => l.Order > previous && l.Order <= upTo).OrderBy(l => l.Order).Select(l => l.Id).ToList();
            previous = Math.Max(previous, upTo);
        }
        return new ClassCurriculum(c.Level == JlptLevel.Beginner ? JlptLevel.N5 : c.Level, milestones, scopes);
    }
}
