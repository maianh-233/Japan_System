using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abroad.Common;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.Abroad;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Students.Application.Abroad.GetAbroadCases;

/// <summary>HV-17 — Học viên › Du học / XKLĐ: hồ sơ theo HV / chương trình / giai đoạn; hồ sơ đã dừng chỉ hiện khi <paramref name="IncludeCancelled"/></summary>
public sealed record GetAbroadCasesQuery(Guid? StudentId, AbroadProgram? Program, AbroadStage? Stage, bool IncludeCancelled)
    : IQuery<IReadOnlyList<AbroadCaseDto>>;

public sealed class GetAbroadCasesHandler(IStudentsDbContext db, StudentAccess access, AbroadDesk desk)
    : IQueryHandler<GetAbroadCasesQuery, IReadOnlyList<AbroadCaseDto>>
{
    public async Task<Result<IReadOnlyList<AbroadCaseDto>>> Handle(GetAbroadCasesQuery q, CancellationToken ct)
    {
        var query = db.AbroadCases.AsQueryable();
        if (access.Branches(StudentAccess.Abroad, PermissionAction.View) is { } allowed)
        {
            var branches = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(c => branches.Contains(c.BranchId));
        }
        if (await access.OwnStudentsAsync(StudentAccess.Abroad, PermissionAction.View, ct) is { } own)
        {
            var mine = own.Select(s => new StudentId(s)).ToList();
            query = query.Where(c => mine.Contains(c.StudentId));
        }
        if (q.StudentId is { } sid) query = query.Where(c => c.StudentId == new StudentId(sid));
        if (q.Program is { } p) query = query.Where(c => c.Program == p);
        if (q.Stage is { } st) query = query.Where(c => c.Stage == st);
        if (!q.IncludeCancelled) query = query.Where(c => c.CancelledAt == null);
        return Result.Success(await desk.ToDtosAsync(query, ct));
    }
}
