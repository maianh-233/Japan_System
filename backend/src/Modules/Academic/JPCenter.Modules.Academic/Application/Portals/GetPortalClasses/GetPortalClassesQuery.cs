using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Academic.Application.Abstractions;
using JPCenter.Modules.Academic.Application.Classes.Common;
using JPCenter.Modules.Academic.Application.Portals.Common;
using JPCenter.Modules.Academic.Domain.Classes;
using JPCenter.Modules.Students.Contracts;
using JPCenter.Modules.Students.Contracts.Dtos;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Academic.Application.Portals.GetPortalClasses;

/// <summary>HV trong lớp hiện cho GV — chỉ mã, họ tên, trạng thái ghi danh (không SĐT / học phí / chăm sóc)</summary>
public sealed record PortalStudentDto(Guid StudentId, Guid EnrollmentId, string Code, string FullName, string Status, DateOnly? ReservedUntil);

public sealed record TeachingClassDto(ClassDto Class, IReadOnlyList<PortalStudentDto> Students);

/// <summary>Con / chính HV của tài khoản cổng</summary>
public sealed record PortalChildDto(Guid Id, string Code, string FullName, DateOnly? Dob, Guid BranchId);

/// <summary>1 ghi danh của HV kèm lớp — cổng HV / PH "Lớp của tôi"</summary>
public sealed record LearningClassDto(Guid StudentId, Guid EnrollmentId, string Status, DateOnly EnrolledAt, DateOnly? EndedAt, DateOnly? ReservedUntil,
    ClassDto Class);

/// <summary>Cổng GV: lớp mình là GV / trợ giảng (kể cả đã kết thúc) + lớp có buổi mình dạy thay, kèm danh sách HV</summary>
public sealed record GetTeachingClassesQuery : IQuery<IReadOnlyList<TeachingClassDto>>;

/// <summary>Cổng HV / PH: HV của tài khoản (HV = chính mình, PH = các con qua liên kết Crm)</summary>
public sealed record GetMyStudentsQuery : IQuery<IReadOnlyList<PortalChildDto>>;

/// <summary>Cổng HV / PH: mọi ghi danh + lớp của HV (<paramref name="StudentId"/> null = mọi con)</summary>
public sealed record GetLearningClassesQuery(Guid? StudentId) : IQuery<IReadOnlyList<LearningClassDto>>;

public sealed class GetPortalClassesHandler(IAcademicDbContext db, ClassReader classes, PortalOwner owner, IEnrollmentLookup enrollments)
    : IQueryHandler<GetTeachingClassesQuery, IReadOnlyList<TeachingClassDto>>, IQueryHandler<GetMyStudentsQuery, IReadOnlyList<PortalChildDto>>,
        IQueryHandler<GetLearningClassesQuery, IReadOnlyList<LearningClassDto>>
{
    public async Task<Result<IReadOnlyList<TeachingClassDto>>> Handle(GetTeachingClassesQuery q, CancellationToken ct)
    {
        if (owner.TeacherId is not { } me) return Result.Success<IReadOnlyList<TeachingClassDto>>([]);
        var own = await db.Classes.AsNoTracking().Where(c => c.Status != ClassStatus.Cancelled)
            .Select(c => new { c.Id, c.TeacherIds, c.AssistantIds }).ToListAsync(ct);
        var ids = own.Where(c => c.TeacherIds.Contains(me) || c.AssistantIds.Contains(me)).Select(c => c.Id).ToHashSet();
        // Lớp có buổi mình dạy thay (GV ghi đè ở buổi)
        var substitute = await db.ClassSessions.AsNoTracking().Where(s => s.TeacherIds.Contains(me)).Select(s => s.ClassId).Distinct().ToListAsync(ct);
        ids.UnionWith(substitute);

        var result = new List<TeachingClassDto>();
        foreach (var cls in await classes.ForPortalAsync(ids, ct))
        {
            var roster = await enrollments.RosterAsync(cls.Id, ct);
            result.Add(new TeachingClassDto(cls,
                roster.Select(r => new PortalStudentDto(r.StudentId, r.EnrollmentId, r.Code, r.FullName, r.Status, r.ReservedUntil)).ToList()));
        }
        return result;
    }

    public async Task<Result<IReadOnlyList<PortalChildDto>>> Handle(GetMyStudentsQuery q, CancellationToken ct) =>
        Result.Success<IReadOnlyList<PortalChildDto>>((await enrollments.SummariesAsync(await owner.StudentsAsync(ct), ct))
            .Select(s => new PortalChildDto(s.Id, s.Code, s.FullName, s.Dob, s.BranchId)).ToList());

    public async Task<Result<IReadOnlyList<LearningClassDto>>> Handle(GetLearningClassesQuery q, CancellationToken ct)
    {
        IReadOnlyList<Guid> students;
        if (q.StudentId is { } one)
        {
            var s = await owner.StudentAsync(one, ct);
            if (s.IsFailure) return s.Error;
            students = [s.Value];
        }
        else students = await owner.StudentsAsync(ct);

        var entries = new List<(Guid StudentId, StudentEnrollmentEntry Entry)>();
        foreach (var sid in students) entries.AddRange((await enrollments.OfStudentAsync(sid, ct)).Select(e => (sid, e)));
        var dtos = (await classes.ForPortalAsync(entries.Select(e => new ClassId(e.Entry.ClassId)).ToList(), ct)).ToDictionary(c => c.Id);
        return entries.Where(e => dtos.ContainsKey(e.Entry.ClassId))
            .Select(e => new LearningClassDto(e.StudentId, e.Entry.EnrollmentId, e.Entry.Status, e.Entry.EnrolledAt, e.Entry.EndedAt,
                e.Entry.ReservedUntil, dtos[e.Entry.ClassId]))
            .ToList();
    }
}
