using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Timesheets.CheckIn;

/// <summary>CC-01 — check-in bằng giờ server (giờ VN), không nhận giờ từ trình duyệt.</summary>
public sealed record CheckInCommand : ICommand<PunchResult>;

/// <summary>Kết quả check-in / check-out: bản ghi + trạng thái hôm nay + cảnh báo ("muộn N phút")</summary>
public sealed record PunchResult(TimeRecordDto Record, TimesheetDayDto Today, string? Warning);

/// <summary>
/// Không gắn hồ sơ / GV → E1 · nghỉ việc / nghỉ dài hạn → E2 · bấm lần 2 trả bản ghi cũ (E4) · nghỉ phép / không ca vẫn chấm (E5).
/// E7: chưa giới hạn IP / GPS — nhật ký ghi IP để đối chiếu.
/// </summary>
public sealed class CheckInHandler(IHrDbContext db, CurrentEmployee current, TimesheetCalculator calculator, IAuditTrail audit, IClock clock)
    : ICommandHandler<CheckInCommand, PunchResult>
{
    public async Task<Result<PunchResult>> Handle(CheckInCommand cmd, CancellationToken ct)
    {
        var found = await current.StaffAsync(TimeRecordErrors.NotStaff, track: false, ct);
        if (found.IsFailure) return found.Error;
        var me = found.Value;
        if (me.Status is EmployeeStatus.Quit or EmployeeStatus.Leave) return TimeRecordErrors.Inactive;

        var today = clock.Today;
        var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.EmployeeId == me.Id && r.Date == today, ct);
        if (record is null)
        {
            var now = TimeOnly.FromDateTime(clock.LocalNow);
            record = TimeRecord.CheckInAt(me.Id, me.BranchId, today, new TimeOnly(now.Hour, now.Minute));
            db.TimeRecords.Add(record);
            await db.SaveChangesAsync(ct);
            audit.Record(AuditCategory.Data, AuditSeverity.Info, "Check-in", me.FullName, $"{today:dd/MM/yyyy} {TimeText.Of(record.CheckIn)}",
                branchId: me.BranchId.Value);
        }

        var day = (await calculator.DaysAsync([me], today, today, ct))[me.Id][0];
        var warning = day.Expected is not null && day.LateMin > 0 && day.State is not (AttendanceState.Leave or AttendanceState.Off)
            ? $"Bạn đi muộn {day.LateMin} phút" : null;
        return new PunchResult(TimesheetCalculator.ToDto(record), day, warning);
    }
}
