using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Assignments.Common;
using JPCenter.Modules.Academic.Application.Learning.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Assignments.GetClassAssignments;

/// <summary>Tab Bài tập của lớp (giáo vụ — <c>academic.classes.view</c>; cổng GV — lớp mình dạy): bài tập + mọi bài nộp</summary>
public sealed record GetClassAssignmentsQuery(Guid ClassId, bool AsTeacher) : IQuery<ClassAssignmentsDto>;

public sealed class GetClassAssignmentsHandler(IAcademicDbContext db, LearningAccess access, AssignmentReader reader)
    : IQueryHandler<GetClassAssignmentsQuery, ClassAssignmentsDto>
{
    public async Task<Result<ClassAssignmentsDto>> Handle(GetClassAssignmentsQuery q, CancellationToken ct)
    {
        var cls = await access.ClassAsync(q.ClassId, q.AsTeacher, PermissionAction.View, ct);
        if (cls.IsFailure) return cls.Error;
        var list = await db.Assignments.AsNoTracking().Where(a => a.ClassId == cls.Value.Id).OrderByDescending(a => a.AssignedAt).ToListAsync(ct);
        return new ClassAssignmentsDto(await reader.ToDtosAsync(list, ct), await reader.SubmissionsAsync(list, null, ct));
    }
}
