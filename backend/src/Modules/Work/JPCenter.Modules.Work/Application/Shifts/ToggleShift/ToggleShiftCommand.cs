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

namespace JPCenter.Modules.Work.Application.Shifts.ToggleShift;

/// <summary>XC-01 — bấm ô: chưa có ca → xếp (<paramref name="Assign"/> = true), có rồi → bỏ (false). Bỏ trống <paramref name="Assign"/> = đảo trạng thái.</summary>
public sealed record ToggleShiftCommand(Guid EmployeeId, DateOnly Date, ShiftKey Shift, bool? Assign) : ICommand<ToggleShiftResult>;

/// <summary><c>assignment</c> null = ô trống sau thao tác; <c>warning</c>: NV nghỉ phép ngày đó (E5)</summary>
public sealed record ToggleShiftResult(bool Assigned, ShiftAssignmentDto? Assignment, string? Warning);

/// <summary>
/// Ngày đã qua → E1 · NV báo bận → E2 · NV không làm theo ca / nghỉ / khác chi nhánh ngoài phạm vi → E3 · đã có → trả ca có sẵn (E4) ·
/// nghỉ phép → vẫn xếp, cảnh báo (E5) · bỏ ca đang có đơn đổi chờ duyệt → E6.
/// </summary>
public sealed class ToggleShiftHandler(IWorkDbContext db, ShiftDesk desk, WorkPeople people, IHrLeaveCalendar leaves, IClock clock)
    : ICommandHandler<ToggleShiftCommand, ToggleShiftResult>
{
    public async Task<Result<ToggleShiftResult>> Handle(ToggleShiftCommand cmd, CancellationToken ct)
    {
        var employee = await people.FindAsync(cmd.EmployeeId, ct);
        if (!ShiftDesk.Assignable(employee)) return ShiftErrors.EmployeeInvalid;
        if (!desk.InBranch(PermissionAction.Update, employee!.BranchId)) return ShiftDesk.BranchNotAllowed;

        var today = clock.Today;
        var existing = await db.ShiftAssignments.FirstOrDefaultAsync(a => a.EmployeeId == cmd.EmployeeId && a.Date == cmd.Date && a.Shift == cmd.Shift, ct);
        var assign = cmd.Assign ?? existing is null;

        if (!assign)
        {
            if (existing is null) return new ToggleShiftResult(false, null, null);
            if (existing.CanRemove(today) is { IsFailure: true } past) return past.Error;
            if ((await leaves.PendingSwapShiftIdsAsync([existing.Id.Value], ct)).Count > 0) return ShiftErrors.SwapPending;
            db.ShiftAssignments.Remove(existing);
            await db.SaveChangesAsync(ct);
            return new ToggleShiftResult(false, null, null);
        }

        if (existing is not null) return new ToggleShiftResult(true, ShiftDesk.ToDto(existing), null);
        var busy = await db.ShiftRegistrations.AnyAsync(r => r.EmployeeId == cmd.EmployeeId && r.Date == cmd.Date && r.Shift == cmd.Shift
            && r.Kind == ShiftRegistrationKind.Busy, ct);
        var created = ShiftAssignment.Create(cmd.EmployeeId, new BranchId(employee.BranchId), cmd.Date, cmd.Shift, today, busy);
        if (created.IsFailure) return created.Error;
        db.ShiftAssignments.Add(created.Value);
        await db.SaveChangesAsync(ct);

        var onLeave = (await leaves.ApprovedLeavesAsync([cmd.EmployeeId], cmd.Date, cmd.Date, ct)).Count > 0;
        return new ToggleShiftResult(true, ShiftDesk.ToDto(created.Value),
            onLeave ? $"{employee.FullName} có đơn nghỉ đã duyệt ngày {cmd.Date:dd/MM}" : null);
    }
}
