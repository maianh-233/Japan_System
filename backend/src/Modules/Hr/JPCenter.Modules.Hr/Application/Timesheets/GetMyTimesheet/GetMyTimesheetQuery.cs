using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Hr.Application.Timesheets.GetMyTimesheet;

/// <summary>CC-02 — bảng công tháng của tôi (<c>YYYY-MM</c>, bỏ trống = tháng này). Không xem tháng tương lai.</summary>
public sealed record GetMyTimesheetQuery(string? Month) : IQuery<MyTimesheetDto>;

public sealed class GetMyTimesheetHandler(CurrentEmployee current, TimesheetCalculator calculator, IClock clock)
    : IQueryHandler<GetMyTimesheetQuery, MyTimesheetDto>
{
    public static readonly Error InvalidMonth = Error.Validation("hr.timesheet.invalid_month", "Tháng không hợp lệ (YYYY-MM, không xem tháng tương lai)");

    public async Task<Result<MyTimesheetDto>> Handle(GetMyTimesheetQuery q, CancellationToken ct)
    {
        var found = await current.StaffAsync(TimeRecordErrors.NotStaff, track: false, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;

        var today = clock.Today;
        var range = TimesheetCalculator.ParseMonth(q.Month ?? TimesheetCalculator.MonthOf(today));
        if (range is not { } r || r.From > today) return InvalidMonth;

        var days = (await calculator.DaysAsync([me], r.From, r.To, ct))[me.Id];
        var todayRow = days.FirstOrDefault(d => d.Date == today);
        return new MyTimesheetDto(TimesheetCalculator.Employee(me), TimesheetCalculator.MonthOf(r.From), days, TimesheetCalculator.Totals(days),
            me.LeaveBalance, days.Where(d => d.Problem).ToList(), todayRow);
    }
}
