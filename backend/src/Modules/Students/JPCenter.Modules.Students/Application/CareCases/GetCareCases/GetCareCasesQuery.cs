using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Students.Application.Abstractions;
using JPCenter.Modules.Students.Application.CareCases.Common;
using JPCenter.Modules.Students.Application.Common;
using JPCenter.Modules.Students.Domain.CareCases;
using JPCenter.Modules.Students.Domain.Students;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Students.Application.CareCases.GetCareCases;

/// <summary>
/// HV-14 — Học viên › Cảnh báo bỏ học: trường hợp chăm sóc theo chi nhánh / lớp / HV / mức / trạng thái.
/// Mặc định ẩn case đã ổn định mà không còn dấu hiệu (<paramref name="IncludeResolved"/> = true để xem cả).
/// </summary>
public sealed record GetCareCasesQuery(Guid? BranchId, Guid? ClassId, Guid? StudentId, RiskLevel? Level, CareStatus? Status, bool IncludeResolved)
    : IQuery<IReadOnlyList<CareCaseDto>>;

public sealed class GetCareCasesHandler(IStudentsDbContext db, StudentAccess access, CareCaseReader reader)
    : IQueryHandler<GetCareCasesQuery, IReadOnlyList<CareCaseDto>>
{
    public async Task<Result<IReadOnlyList<CareCaseDto>>> Handle(GetCareCasesQuery q, CancellationToken ct)
    {
        var query = db.CareCases.AsNoTracking();
        if (access.Branches(StudentAccess.AtRisk, PermissionAction.View) is { } allowed)
        {
            var branches = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(c => branches.Contains(c.BranchId));
        }
        if (q.BranchId is { } b) query = query.Where(c => c.BranchId == new BranchId(b));
        if (q.ClassId is { } cid) query = query.Where(c => c.ClassId == cid);
        if (q.StudentId is { } sid) query = query.Where(c => c.StudentId == new StudentId(sid));
        if (q.Level is { } level) query = query.Where(c => c.RiskLevel == level);
        if (q.Status is { } st) query = query.Where(c => c.Status == st);
        else if (!q.IncludeResolved) query = query.Where(c => c.Status != CareStatus.Resolved || c.Flags.Count > 0);
        return Result.Success(await reader.ToDtosAsync(await query.ToListAsync(ct), ct));
    }
}
