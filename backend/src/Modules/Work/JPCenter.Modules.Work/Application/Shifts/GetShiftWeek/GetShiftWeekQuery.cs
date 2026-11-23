using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Common;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Shifts.GetShiftWeek;

/// <summary>
/// XC-01 / XC-03 — lưới xếp ca tuần chứa <paramref name="Week"/> (bỏ trống = tuần này): NV, ca, rảnh / bận, ngày nghỉ, đủ người.
/// <paramref name="BranchId"/> bỏ trống = chi nhánh của người đang xem.
/// </summary>
public sealed record GetShiftWeekQuery(Guid? BranchId, DateOnly? Week) : IQuery<ShiftWeekDto>;

public sealed class GetShiftWeekHandler(IWorkDbContext db, ShiftDesk desk, WorkPeople people, IHrLeaveCalendar leaves, IClock clock)
    : IQueryHandler<GetShiftWeekQuery, ShiftWeekDto>
{
    public async Task<Result<ShiftWeekDto>> Handle(GetShiftWeekQuery query, CancellationToken ct)
    {
        var branchId = query.BranchId ?? (await people.MeAsync(ct))?.BranchId;
        if (branchId is not { } b || !desk.InBranch(PermissionAction.View, b)) return ShiftDesk.BranchNotAllowed;
        var q = query with { BranchId = b };
        var from = ShiftDesk.WeekStart(q.Week ?? clock.Today);
        var to = from.AddDays(6);
        var staff = await desk.ShiftStaffAsync(b, ct);
        var ids = staff.Select(e => e.Id).ToList();
        var branch = new BranchId(b);

        var assignments = await db.ShiftAssignments.AsNoTracking()
            .Where(a => a.BranchId == branch && a.Date >= from && a.Date <= to).ToListAsync(ct);
        var registrations = await db.ShiftRegistrations.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Date >= from && r.Date <= to).ToListAsync(ct);
        var leaveDays = (await leaves.ApprovedLeavesAsync(ids, from, to, ct))
            .SelectMany(l => Enumerable.Range(0, l.To.DayNumber - l.From.DayNumber + 1).Select(i => l.From.AddDays(i))
                .Where(d => d >= from && d <= to).Select(d => new LeaveDayDto(l.EmployeeId, d, l.Type)))
            .DistinctBy(l => (l.EmployeeId, l.Date)).ToList();

        var deptOf = staff.ToDictionary(e => e.Id, e => e.Department);
        var coverage = new List<CoverageDto>();
        for (var d = from; d <= to; d = d.AddDays(1))
            foreach (var shift in ShiftDesk.Shifts)
            {
                var count = assignments.Count(a => a.Date == d && a.Shift == shift);
                var min = deptOf.Values.Distinct().Sum(dep => ShiftCoverage.Minimum(dep, d, shift));
                coverage.Add(new CoverageDto(d, shift, count, min, count < min));
            }

        return new ShiftWeekDto(b, from,
            staff.Select(e => new ShiftEmployeeDto(e.Id, e.Code, e.FullName, e.Department, e.Position, e.Status)).ToList(),
            assignments.OrderBy(a => a.Date).ThenBy(a => a.Shift).Select(ShiftDesk.ToDto).ToList(),
            registrations.Select(ShiftDesk.ToDto).ToList(), leaveDays, coverage, desk.InBranch(PermissionAction.Update, b));
    }
}
