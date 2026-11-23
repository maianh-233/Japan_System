using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Work.Application.Abstractions;
using JPCenter.Modules.Work.Application.Shifts.Common;
using JPCenter.Modules.Work.Domain.Shifts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Work.Application.Shifts.FillWeek;

/// <summary>Cách xếp nhanh cả tuần (XC-02)</summary>
public enum FillMode
{
    /// <summary>Mọi ca NV báo rảnh (từ hôm nay) chưa xếp → xếp</summary>
    FromRegistrations,
    /// <summary>Ca tuần trước → cùng thứ tuần này, chỉ ô còn trống; bỏ qua ca NV báo bận, ngày đã qua, NV không còn xếp được</summary>
    CopyPreviousWeek,
}

/// <summary>XC-02 — xếp theo đăng ký / chép tuần trước, 1 request. Trả số ca thêm.</summary>
public sealed record FillWeekCommand(Guid BranchId, DateOnly? Week, FillMode Mode) : ICommand<int>;

public sealed class FillWeekHandler(IWorkDbContext db, ShiftDesk desk, IClock clock) : ICommandHandler<FillWeekCommand, int>
{
    public async Task<Result<int>> Handle(FillWeekCommand cmd, CancellationToken ct)
    {
        if (!desk.InBranch(PermissionAction.Update, cmd.BranchId)) return ShiftDesk.BranchNotAllowed;
        var today = clock.Today;
        var from = ShiftDesk.WeekStart(cmd.Week ?? today);
        var to = from.AddDays(6);
        var staff = (await desk.ShiftStaffAsync(cmd.BranchId, ct)).Where(ShiftDesk.Assignable).Select(e => e.Id).ToHashSet();
        var ids = staff.ToList();
        var branch = new BranchId(cmd.BranchId);

        var taken = (await db.ShiftAssignments.AsNoTracking()
                .Where(a => ids.Contains(a.EmployeeId) && a.Date >= from && a.Date <= to).Select(a => new { a.EmployeeId, a.Date, a.Shift }).ToListAsync(ct))
            .Select(a => (a.EmployeeId, a.Date, a.Shift)).ToHashSet();
        var regs = await db.ShiftRegistrations.AsNoTracking()
            .Where(r => ids.Contains(r.EmployeeId) && r.Date >= from && r.Date <= to).ToListAsync(ct);
        var busy = regs.Where(r => r.Kind == ShiftRegistrationKind.Busy).Select(r => (r.EmployeeId, r.Date, r.Shift)).ToHashSet();

        IEnumerable<(Guid EmployeeId, DateOnly Date, ShiftKey Shift)> wanted;
        if (cmd.Mode == FillMode.FromRegistrations)
            wanted = regs.Where(r => r.Kind == ShiftRegistrationKind.Available).Select(r => (r.EmployeeId, r.Date, r.Shift));
        else
        {
            var prevFrom = from.AddDays(-7);
            var prevTo = from.AddDays(-1);
            wanted = (await db.ShiftAssignments.AsNoTracking()
                    .Where(a => a.BranchId == branch && a.Date >= prevFrom && a.Date <= prevTo).Select(a => new { a.EmployeeId, a.Date, a.Shift })
                    .ToListAsync(ct))
                .Select(a => (a.EmployeeId, a.Date.AddDays(7), a.Shift));
        }

        var added = 0;
        foreach (var slot in wanted.Distinct())
        {
            if (slot.Date < today || !staff.Contains(slot.EmployeeId) || taken.Contains(slot) || busy.Contains(slot)) continue;
            var created = ShiftAssignment.Create(slot.EmployeeId, branch, slot.Date, slot.Shift, today, busy: false);
            if (created.IsFailure) continue;
            db.ShiftAssignments.Add(created.Value);
            taken.Add(slot);
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }
}
