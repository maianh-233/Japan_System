using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.UnlockPayroll;

/// <summary>
/// TC-13.E2 — mở khóa bảng lương các chi nhánh để sửa (nhật ký <c>warning</c> ở endpoint). Chi nhánh nào đã chi lương → chặn cả lô
/// (<c>finance.payroll.already_paid</c>, E3); không chi nhánh nào đang khóa → <c>finance.payroll.not_locked</c>.
/// </summary>
public sealed record UnlockPayrollCommand(PayrollKind Kind, string? Month, IReadOnlyList<Guid>? BranchIds) : ICommand<PayrollActionResult>;

public sealed class UnlockPayrollHandler(IFinanceDbContext db, PayrollBoards boards, FinanceAccess access, IClock clock)
    : ICommandHandler<UnlockPayrollCommand, PayrollActionResult>
{
    public async Task<Result<PayrollActionResult>> Handle(UnlockPayrollCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var month = PayrollMonth.Parse(cmd.Month);
        if (month.IsFailure) return month.Error;
        var branches = await boards.BranchesAsync(cmd.Kind, PermissionAction.Update, cmd.BranchIds, ct);
        if (branches.IsFailure) return branches.Error;

        var ids = branches.Value.Select(b => new BranchId(b)).ToList();
        var locks = (await db.PayrollLocks.Where(l => l.Kind == cmd.Kind && l.Month == month.Value && ids.Contains(l.BranchId)).ToListAsync(ct))
            .Where(l => l.IsLocked).ToList();
        if (locks.Count == 0) return PayrollErrors.NotLocked;
        if (locks.Any(l => l.IsPaid)) return PayrollErrors.AlreadyPaid;
        foreach (var l in locks)
        {
            var r = l.Unlock(actor, clock.UtcNow);
            if (r.IsFailure) return r.Error;
        }
        await db.SaveChangesAsync(ct);
        return new PayrollActionResult(await boards.LockDtosAsync(locks, ct), []);
    }
}
