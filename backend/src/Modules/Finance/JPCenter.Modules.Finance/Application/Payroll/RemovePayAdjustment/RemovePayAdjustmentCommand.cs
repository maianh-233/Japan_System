using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Payroll.Common;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Payroll.RemovePayAdjustment;

/// <summary>Xóa (mềm) 1 điều chỉnh lương — tháng đã chốt → <c>finance.payroll.locked</c> (TC-13.E2)</summary>
public sealed record RemovePayAdjustmentCommand(Guid Id) : ICommand;

public sealed class RemovePayAdjustmentHandler(IFinanceDbContext db, PayrollGuard guard) : ICommandHandler<RemovePayAdjustmentCommand>
{
    public static readonly Error NotFound = Error.NotFound("finance.pay_adjust.not_found", "Không tìm thấy điều chỉnh lương");

    public async Task<Result> Handle(RemovePayAdjustmentCommand cmd, CancellationToken ct)
    {
        var id = new PayAdjustmentId(cmd.Id);
        var a = await db.PayAdjustments.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (a is null) return NotFound;
        var allowed = await guard.CanAdjustAsync(a.EmployeeId, a.Month, ct);
        if (allowed.IsFailure) return allowed.Error;
        db.PayAdjustments.Remove(a);
        await db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
