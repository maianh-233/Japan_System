using JPCenter.Modules.Work.Contracts;
using JPCenter.Modules.Work.Contracts.Dtos;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.Modules.Work.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Infrastructure.Services;

/// <summary>Hiện thực <see cref="IShiftSchedule"/> cho Hr (chấm công theo ca, đơn đổi ca).</summary>
internal sealed class ShiftSchedule(WorkDbContext db) : IShiftSchedule
{
    public async Task<IReadOnlyList<ScheduledShift>> OfEmployeesAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var ids = employeeIds.Distinct().ToList();
        if (ids.Count == 0) return [];
        var rows = await db.ShiftAssignments.AsNoTracking().Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to).ToListAsync(ct);
        return rows.Select(ToDto).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, ScheduledShift>> FindManyAsync(IReadOnlyCollection<Guid> shiftIds, CancellationToken ct)
    {
        var ids = shiftIds.Distinct().Select(x => new ShiftAssignmentId(x)).ToList();
        var rows = await db.ShiftAssignments.AsNoTracking().Where(a => ids.Contains(a.Id)).ToListAsync(ct);
        return rows.ToDictionary(a => a.Id.Value, ToDto);
    }

    private static ScheduledShift ToDto(ShiftAssignment a)
    {
        var (from, to, s) = WorkShiftTimes.Of(a.Shift);
        return new ScheduledShift(a.Id.Value, a.EmployeeId, a.BranchId.Value, a.Date, a.Shift, from, to, s);
    }
}
