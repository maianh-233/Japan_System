using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Application.Timesheets.GetMyTimesheet;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Timesheets.GetTimesheets;

/// <summary>
/// CC-03 — lưới nhân viên (kind = staff) × ngày của tháng trong phạm vi <c>hr.timesheets.view</c>. Nghỉ việc trong / sau tháng vẫn hiện (E1);
/// nghỉ trước tháng thì không. <paramref name="ProblemsOnly"/>: chỉ người có vi phạm.
/// </summary>
public sealed record GetTimesheetsQuery(string? Month, Guid? BranchId, Department? Department, bool ProblemsOnly) : IQuery<TimesheetBoardDto>;

public sealed class GetTimesheetsHandler(IHrDbContext db, HrScope scope, TimesheetCalculator calculator, IClock clock)
    : IQueryHandler<GetTimesheetsQuery, TimesheetBoardDto>
{
    public async Task<Result<TimesheetBoardDto>> Handle(GetTimesheetsQuery q, CancellationToken ct)
    {
        var today = clock.Today;
        var range = TimesheetCalculator.ParseMonth(q.Month ?? TimesheetCalculator.MonthOf(today));
        if (range is not { } r || r.From > today) return GetMyTimesheetHandler.InvalidMonth;

        var all = await db.Employees.AsNoTracking().ToListAsync(ct);
        var staff = scope.Employees(all, HrScope.Timesheets, PermissionAction.View)
            .Where(e => e.Kind == EmployeeKind.Staff && e.JoinedAt <= r.To && (!e.HasQuit || e.QuitAt is not { } quit || quit >= r.From))
            .Where(e => q.BranchId is not { } b || e.BranchId.Value == b)
            .Where(e => q.Department is not { } d || e.Department == d)
            .OrderBy(e => e.Department).ThenBy(e => e.FullName)
            .ToList();

        var days = await calculator.DaysAsync(staff, r.From, r.To, ct);
        var rows = staff.Select(e => new TimesheetRowDto(TimesheetCalculator.Employee(e), days[e.Id], TimesheetCalculator.Totals(days[e.Id])))
            .Where(row => !q.ProblemsOnly || row.Days.Any(d => d.Problem))
            .ToList();
        return new TimesheetBoardDto(TimesheetCalculator.MonthOf(r.From), rows);
    }
}
