using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IEmployeeDirectory"/> cho module khác đọc nhân viên.</summary>
internal sealed class EmployeeDirectory(HrDbContext db) : IEmployeeDirectory
{
    public async Task<EmployeeSummary?> FindAsync(Guid employeeId, CancellationToken ct)
    {
        var id = new EmployeeId(employeeId);
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : ToSummary(e);
    }

    public async Task<IReadOnlyDictionary<Guid, EmployeeSummary>> FindManyAsync(IReadOnlyCollection<Guid> employeeIds, CancellationToken ct)
    {
        var ids = employeeIds.Distinct().Select(x => new EmployeeId(x)).ToList();
        var list = await db.Employees.AsNoTracking().Where(x => ids.Contains(x.Id)).ToListAsync(ct);
        return list.ToDictionary(x => x.Id.Value, ToSummary);
    }

    public async Task<IReadOnlyList<EmployeeSummary>> ListActiveAsync(CancellationToken ct)
    {
        var list = await db.Employees.AsNoTracking()
            .Where(x => x.Status != EmployeeStatus.Quit)
            .OrderBy(x => x.FullName)
            .ToListAsync(ct);
        return list.Select(ToSummary).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, BranchHeadcount>> HeadcountByBranchAsync(CancellationToken ct)
    {
        var rows = await db.Employees.AsNoTracking()
            .Where(x => x.Status != EmployeeStatus.Quit)
            .GroupBy(x => x.BranchId)
            .Select(g => new { Branch = g.Key, Staff = g.Count(), Teachers = g.Count(x => x.Kind == EmployeeKind.Teacher) })
            .ToListAsync(ct);
        return rows.ToDictionary(r => r.Branch.Value, r => new BranchHeadcount(r.Staff, r.Teachers));
    }

    private static EmployeeSummary ToSummary(Employee e) => new(
        e.Id.Value, e.Code, e.FullName, e.Email,
        SnakeCaseEnumConverter<EmployeeKind>.ToText(e.Kind), e.BranchId.Value,
        SnakeCaseEnumConverter<EmployeeStatus>.ToText(e.Status), e.Position,
        SnakeCaseEnumConverter<Department>.ToText(e.Department));
}
