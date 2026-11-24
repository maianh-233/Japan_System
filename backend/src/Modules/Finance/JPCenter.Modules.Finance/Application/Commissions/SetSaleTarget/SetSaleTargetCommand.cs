using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Commissions.SetSaleTarget;

/// <summary>TC-12 bước 2 — đặt / sửa chỉ tiêu tháng của 1 Sale (UQ nhân viên + tháng). Chỉ tiêu âm / lẻ → <c>finance.commission.invalid_target</c>.</summary>
public sealed record SetSaleTargetCommand(Guid EmployeeId, string? Month, decimal Target) : ICommand<SaleTargetDto>;

public sealed class SetSaleTargetHandler(IFinanceDbContext db, IPayrollSource hr, FinanceAccess access) : ICommandHandler<SetSaleTargetCommand, SaleTargetDto>
{
    public static readonly Error NotSale = Error.Validation("finance.commission.not_sale", "Nhân viên này không thuộc phòng Sale");

    public async Task<Result<SaleTargetDto>> Handle(SetSaleTargetCommand cmd, CancellationToken ct)
    {
        var month = PayrollMonth.Parse(cmd.Month);
        if (month.IsFailure) return month.Error;
        var sale = await hr.ProfileAsync(cmd.EmployeeId, ct);
        if (sale is null || sale.Department != CommissionCalculator.SalesDepartment) return NotSale;
        if (!access.InBranch(FinanceAccess.Commissions, PermissionAction.Update, sale.BranchId)) return FinanceAccess.BranchNotAllowed;

        var target = await db.SaleTargets.FirstOrDefaultAsync(t => t.EmployeeId == cmd.EmployeeId && t.Month == month.Value, ct);
        if (target is null)
        {
            var created = SaleTarget.Create(cmd.EmployeeId, month.Value, cmd.Target);
            if (created.IsFailure) return created.Error;
            target = created.Value;
            db.SaleTargets.Add(target);
        }
        else
        {
            var changed = target.Change(cmd.Target);
            if (changed.IsFailure) return changed.Error;
        }
        await db.SaveChangesAsync(ct);
        return new SaleTargetDto(target.EmployeeId, target.Month, target.Target.Amount);
    }
}
