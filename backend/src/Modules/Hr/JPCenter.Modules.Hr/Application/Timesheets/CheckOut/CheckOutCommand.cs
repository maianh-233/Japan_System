using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.CheckIn;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Timesheets.CheckOut;

/// <summary>CC-01 — check-out (giờ server). Bấm lại → cập nhật giờ ra. Chưa check-in hôm nay → E3 (qua nửa đêm là ngày mới — E6).</summary>
public sealed record CheckOutCommand : ICommand<PunchResult>;

public sealed class CheckOutHandler(IHrDbContext db, CurrentEmployee current, TimesheetCalculator calculator, IAuditTrail audit, IClock clock)
    : ICommandHandler<CheckOutCommand, PunchResult>
{
    public async Task<Result<PunchResult>> Handle(CheckOutCommand cmd, CancellationToken ct)
    {
        var found = await current.StaffAsync(TimeRecordErrors.NotStaff, track: false, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (me.Status is EmployeeStatus.Quit or EmployeeStatus.Leave) return TimeRecordErrors.Inactive;

        var today = clock.Today;
        var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.EmployeeId == me.Id && r.Date == today, ct);
        if (record is null) return TimeRecordErrors.NotCheckedIn;
        var now = TimeOnly.FromDateTime(clock.LocalNow);
        var done = record.CheckOutAt(new TimeOnly(now.Hour, now.Minute));
        if (done.IsFailure) return done.Error;
        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Info, "Check-out", me.FullName, $"{today:dd/MM/yyyy} {TimeText.Of(record.CheckOut)}",
            branchId: me.BranchId.Value);

        var day = (await calculator.DaysAsync([me], today, today, ct))[me.Id][0];
        var warning = day.Expected is not null && day.EarlyMin > 0 && day.State is not (AttendanceState.Leave or AttendanceState.Off)
            ? $"Bạn về sớm {day.EarlyMin} phút — có thể gửi đơn đi muộn / về sớm để giải trình" : null;
        return new PunchResult(TimesheetCalculator.ToDto(record), day, warning);
    }
}
