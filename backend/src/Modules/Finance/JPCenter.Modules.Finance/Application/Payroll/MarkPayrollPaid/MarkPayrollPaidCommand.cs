using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.MarkPayrollPaid;

/// <summary>
/// TC-13 bước 4 — "Đã chi lương": mọi chi nhánh xét phải đang khóa (<c>finance.payroll.not_locked</c>) và chưa chi (<c>already_paid</c>) — cả lô hoặc không.
/// Sau đó không mở khóa được nữa (E3).
/// </summary>
public sealed record MarkPayrollPaidCommand(PayrollKind Kind, string? Month, IReadOnlyList<Guid>? BranchIds) : ICommand<PayrollActionResult>;

public sealed class MarkPayrollPaidHandler(IFinanceDbContext db, PayrollBoards boards, IClock clock)
    : ICommandHandler<MarkPayrollPaidCommand, PayrollActionResult>
{
    public async Task<Result<PayrollActionResult>> Handle(MarkPayrollPaidCommand cmd, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(cmd.Month);
        if (month.IsFailure) return month.Error;
        var branches = await boards.BranchesAsync(cmd.Kind, PermissionAction.Update, cmd.BranchIds, ct);
        if (branches.IsFailure) return branches.Error;

        var ids = branches.Value.Select(b => new BranchId(b)).ToList();
        var locks = await db.PayrollLocks.Where(l => l.Kind == cmd.Kind && l.Month == month.Value && ids.Contains(l.BranchId))
            .ToDictionaryAsync(l => l.BranchId.Value, ct);
        if (branches.Value.Count == 0 || branches.Value.Any(b => locks.GetValueOrDefault(b) is not { IsLocked: true })) return PayrollErrors.NotLocked;
        foreach (var l in locks.Values)
        {
            var r = l.MarkPaid(clock.UtcNow);
            if (r.IsFailure) return r.Error;
        }
        await db.SaveChangesAsync(ct);
        return new PayrollActionResult(await boards.LockDtosAsync(locks.Values, ct), []);
    }
}
