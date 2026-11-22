using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.Modules.Academic.Domain.Assignments;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.GetMyAssignments;

/// <summary>
/// ND-02 / ND-11 — cổng HV / PH: bài tập các lớp HV đã ghi danh + bài nộp, điểm, nhận xét của HV (PH chọn con).
/// Quiz không kèm câu hỏi — bấm làm mới phát đề. <c>CanSubmit</c>: HV (không phải PH) đang học lớp, còn lượt / chưa chấm.
/// </summary>
public sealed record GetMyAssignmentsQuery(Guid? StudentId) : IQuery<IReadOnlyList<MyAssignmentDto>>;

public sealed class GetMyAssignmentsHandler(IAcademicDbContext db, LearningAccess access, AssignmentReader reader)
    : IQueryHandler<GetMyAssignmentsQuery, IReadOnlyList<MyAssignmentDto>>
{
    public async Task<Result<IReadOnlyList<MyAssignmentDto>>> Handle(GetMyAssignmentsQuery q, CancellationToken ct)
    {
        var learner = await access.LearnerAsync(q.StudentId, ct);
        if (learner.IsFailure) return learner.Error;
        var me = learner.Value;
        var classIds = me.ClassIds.Select(c => new ClassId(c)).ToList();
        var classes = await db.Classes.AsNoTracking().IgnoreQueryFilters().Where(c => classIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => (c.Code, c.Name), ct);
        var list = await db.Assignments.AsNoTracking().Where(a => classIds.Contains(a.ClassId)).OrderByDescending(a => a.DueDate).ToListAsync(ct);
        var dtos = await reader.ToDtosAsync(list, ct);
        var mine = (await reader.SubmissionsAsync(list, me.StudentId, ct)).ToDictionary(s => s.AssignmentId);
        var graded = await db.Submissions.AsNoTracking()
            .Where(s => s.StudentId == me.StudentId && s.GradedBy != null).Select(s => s.AssignmentId).ToListAsync(ct);

        return list.Select((a, i) =>
        {
            var sub = mine.GetValueOrDefault(a.Id.Value);
            int? left = a.Quiz is { } quiz ? Math.Max(0, quiz.Attempts - (sub?.Quiz?.Attempts ?? 0)) : null;
            var open = !access.IsParent && me.Studies(a.ClassId.Value)
                       && (a.Type == AssignmentType.Quiz ? left > 0 : !graded.Contains(a.Id));
            var (code, name) = classes.GetValueOrDefault(a.ClassId, ("—", "—"));
            return new MyAssignmentDto(dtos[i], code, name, sub, left, open);
        }).ToList();
    }
}
