using FluentValidation;
using JPCenter.Application.Common.Auditing;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Application.Common.Time;
using JPCenter.Modules.Hr.Application.Abstractions;
using JPCenter.Modules.Hr.Application.Common;
using JPCenter.Modules.Hr.Application.Timesheets.Common;
using JPCenter.Modules.Hr.Domain.Employees;
using JPCenter.Modules.Hr.Domain.TimeRecords;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Hr.Application.Timesheets.EditTimeRecord;

/// <summary>CC-04 — sửa giờ vào / ra (bỏ trống được) của 1 ngày, lý do bắt buộc, ghi người sửa.</summary>
public sealed record EditTimeRecordCommand(Guid EmployeeId, DateOnly Date, TimeOnly? CheckIn, TimeOnly? CheckOut, string Reason)
    : ICommand<TimesheetDayDto>;

public sealed class EditTimeRecordValidator : AbstractValidator<EditTimeRecordCommand>
{
    public EditTimeRecordValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

/// <summary>Lý do trống → E1 · giờ ra trước giờ vào → E2 · ngày tương lai → E3 · chưa có bản ghi → tạo mới (E4) · sửa của chính mình → E5.</summary>
public sealed class EditTimeRecordHandler(IHrDbContext db, HrScope scope, CurrentEmployee current, TimesheetCalculator calculator, IAuditTrail audit,
    IClock clock) : ICommandHandler<EditTimeRecordCommand, TimesheetDayDto>
{
    public static readonly Error NotFound = Error.NotFound("hr.time_record.employee_not_found", "Không tìm thấy nhân viên trong phạm vi của bạn");

    public async Task<Result<TimesheetDayDto>> Handle(EditTimeRecordCommand cmd, CancellationToken ct)
    {
        if (current.Id == cmd.EmployeeId) return TimeRecordErrors.SelfEdit;
        var all = await db.Employees.AsNoTracking().ToListAsync(ct);
        var employee = scope.Employees(all, HrScope.Timesheets, PermissionAction.Update)
            .FirstOrDefault(e => e.Id.Value == cmd.EmployeeId && e.Kind == EmployeeKind.Staff);
        if (employee is null) return NotFound;

        var by = current.Id ?? Guid.Empty;
        var today = clock.Today;
        var checkIn = Minute(cmd.CheckIn);
        var checkOut = Minute(cmd.CheckOut);
        var reason = cmd.Reason ?? "";
        var record = await db.TimeRecords.FirstOrDefaultAsync(r => r.EmployeeId == employee.Id && r.Date == cmd.Date, ct);
        if (record is null)
        {
            var created = TimeRecord.Manual(employee.Id, employee.BranchId, cmd.Date, checkIn, checkOut, by, current.Name, reason, today);
            if (created.IsFailure) return created.Error;
            db.TimeRecords.Add(created.Value);
        }
        else if (record.Edit(checkIn, checkOut, by, current.Name, reason, today) is { IsFailure: true } bad) return bad.Error;

        await db.SaveChangesAsync(ct);
        audit.Record(AuditCategory.Data, AuditSeverity.Warning, "Sửa chấm công", employee.FullName,
            $"{cmd.Date:dd/MM/yyyy}: vào {TimeText.Of(checkIn) ?? "—"}, ra {TimeText.Of(checkOut) ?? "—"} · {reason.Trim()}",
            branchId: employee.BranchId.Value);
        return (await calculator.DaysAsync([employee], cmd.Date, cmd.Date, ct))[employee.Id][0];
    }

    private static TimeOnly? Minute(TimeOnly? t) => t is { } v ? new TimeOnly(v.Hour, v.Minute) : null;
}
