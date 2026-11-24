using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Commissions.Common;
using JPCenter.Modules.Finance.Application.Commissions.SetSaleTarget;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Commissions.ReassignSale;

/// <summary>
/// TC-12 bước 4 — kế toán gán lại Sale được ghi nhận cho 1 hóa đơn (ghi đè, không bị hệ thống tự chọn lại). Nhật ký ở endpoint.
/// Không đổi số liệu bảng lương đã chốt (snapshot). Người được gán phải thuộc phòng Sale, chưa nghỉ việc.
/// </summary>
public sealed record ReassignSaleCommand(Guid InvoiceId, Guid EmployeeId) : ICommand<ReassignSaleResult>;

public sealed record ReassignSaleResult(Guid InvoiceId, Guid SaleId, string SaleName);

public sealed class ReassignSaleHandler(IFinanceDbContext db, IPayrollSource hr, FinanceAccess access) : ICommandHandler<ReassignSaleCommand, ReassignSaleResult>
{
    public async Task<Result<ReassignSaleResult>> Handle(ReassignSaleCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var id = new InvoiceId(cmd.InvoiceId);
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == id, ct);
        if (invoice is null || !access.InBranch(FinanceAccess.Commissions, PermissionAction.View, invoice.BranchId.Value)) return InvoiceErrors.NotFound;
        if (!access.InBranch(FinanceAccess.Commissions, PermissionAction.Update, invoice.BranchId.Value)) return FinanceAccess.BranchNotAllowed;
        var sale = await hr.ProfileAsync(cmd.EmployeeId, ct);
        if (sale is null || sale.Department != CommissionCalculator.SalesDepartment || sale.Status == "quit") return SetSaleTargetHandler.NotSale;

        var r = invoice.ReassignSale(sale.Id, actor);
        if (r.IsFailure) return r.Error;
        await db.SaveChangesAsync(ct);
        return new ReassignSaleResult(invoice.Id.Value, sale.Id, sale.FullName);
    }
}
