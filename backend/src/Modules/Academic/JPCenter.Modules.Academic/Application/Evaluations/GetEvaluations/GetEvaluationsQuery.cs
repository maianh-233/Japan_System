using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Common;
using JPCenter.Modules.Academic.Application.Evaluations.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Academic.Domain.ClassSessions;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Evaluations.GetEvaluations;

/// <summary>DT-19 — nhận xét cuối khóa của lớp: nhân viên (<c>academic.classes.view</c>, hồ sơ HV / CRM-19) hoặc GV của lớp trên cổng</summary>
public sealed record GetEvaluationsQuery(Guid ClassId, bool AsTeacher) : IQuery<IReadOnlyList<EvaluationDto>>;

public sealed class GetEvaluationsHandler(IAcademicDbContext db, ClassReader classes, PortalOwner owner, EvaluationReader reader, IClock clock)
    : IQueryHandler<GetEvaluationsQuery, IReadOnlyList<EvaluationDto>>
{
    public async Task<Result<IReadOnlyList<EvaluationDto>>> Handle(GetEvaluationsQuery q, CancellationToken ct)
    {
        Class? cls;
        if (q.AsTeacher)
        {
            cls = await db.Classes.AsNoTracking().FirstOrDefaultAsync(c => c.Id == new ClassId(q.ClassId), ct);
            if (cls is null) return ClassErrors.NotFound;
            if (owner.TeacherId is not { } me || !cls.TeacherIds.Contains(me)) return SessionErrors.NotAssigned;
        }
        else cls = await classes.ForUpdateAsync(q.ClassId, AcademicAccess.Classes, PermissionAction.View, ct);
        if (cls is null) return ClassErrors.NotFound;
        return Result.Success(await reader.OfClassAsync(cls, clock.Today, ct));
    }
}
