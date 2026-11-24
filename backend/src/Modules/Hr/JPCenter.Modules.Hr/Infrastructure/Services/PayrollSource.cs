using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.Modules.Hr.Contracts.Dtos;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.HrRequests;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.Modules.Hr.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IPayrollSource"/> — hồ sơ lương (hợp đồng, đơn giá GV) + công tháng tính từ bảng công
/// (<see cref="TimesheetCalculator"/>, cùng quy tắc trang Chấm công) cho Finance tính lương (TC-13, TC-14).
/// </summary>
internal sealed class PayrollSource(HrDbContext db, TimesheetCalculator timesheets) : IPayrollSource
{
    public async Task<IReadOnlyList<PayProfile>> ProfilesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        var list = await db.Employees.AsNoTracking()
            .Where(e => e.JoinedAt <= to && (e.Status != EmployeeStatus.Quit || (e.QuitAt != null && e.QuitAt >= from)))
            .OrderBy(e => e.FullName)
            .ToListAsync(ct);
        return list.Select(ToProfile).ToList();
    }

    public async Task<PayProfile?> ProfileAsync(Guid employeeId, CancellationToken ct)
    {
        var id = new EmployeeId(employeeId);
        var e = await db.Employees.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return e is null ? null : ToProfile(e);
    }

    public async Task<IReadOnlyDictionary<Guid, WorkDaysSummary>> WorkDaysAsync(IReadOnlyCollection<Guid> employeeIds, DateOnly from, DateOnly to,
        CancellationToken ct)
    {
        var ids = employeeIds.Distinct().Select(i => new EmployeeId(i)).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, WorkDaysSummary>();
        var employees = await db.Employees.AsNoTracking().Where(e => ids.Contains(e.Id)).ToListAsync(ct);
        var withRecords = (await db.TimeRecords.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Date >= from && r.Date <= to)
            .Select(r => r.EmployeeId).Distinct().ToListAsync(ct)).ToHashSet();
        var leaves = (await db.HrRequests.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Status == HrRequestStatus.Approved && r.From <= to && r.To >= from
                && (r.Type == HrRequestType.Leave || r.Type == HrRequestType.BusinessTrip))
            .ToListAsync(ct)).ToLookup(r => r.EmployeeId);
        var days = await timesheets.DaysAsync(employees, from, to, ct);

        var result = new Dictionary<Guid, WorkDaysSummary>();
        foreach (var e in employees)
        {
            int worked = 0, paid = 0, unpaid = 0, absent = 0, late = 0, noOut = 0;
            foreach (var d in days.GetValueOrDefault(e.Id) ?? [])
            {
                switch (d.State)
                {
                    case AttendanceState.Ok or AttendanceState.Early or AttendanceState.Working: worked++; break;
                    case AttendanceState.Late or AttendanceState.LateEarly: worked++; late++; break;
                    case AttendanceState.NoOut: worked++; noOut++; break;
                    case AttendanceState.Absent: absent++; break;
                    case AttendanceState.Leave when d.Date.DayOfWeek != DayOfWeek.Sunday:
                        var r = leaves[e.Id].FirstOrDefault(x => x.Covers(d.Date));
                        if (r is { Type: HrRequestType.BusinessTrip } or { PaidLeave: true }) paid++;
                        else unpaid++;
                        break;
                }
            }
            result[e.Id.Value] = new WorkDaysSummary(worked, paid, unpaid, absent, late, noOut, withRecords.Contains(e.Id));
        }
        return result;
    }

    private static PayProfile ToProfile(Employee e) => new(
        e.Id.Value, e.Code, e.FullName, e.Email,
        SnakeCaseEnumConverter<EmployeeKind>.ToText(e.Kind), SnakeCaseEnumConverter<Department>.ToText(e.Department),
        SnakeCaseEnumConverter<EmployeeStatus>.ToText(e.Status), e.BranchId.Value, e.JoinedAt, e.QuitAt,
        e.Contract.BaseSalary?.Amount ?? 0, e.Contract.Allowance?.Amount ?? 0,
        e.Teacher?.RateOffline.Amount ?? 0, e.Teacher?.RateOnline.Amount ?? 0,
        e.Status == EmployeeStatus.Probation);
}
