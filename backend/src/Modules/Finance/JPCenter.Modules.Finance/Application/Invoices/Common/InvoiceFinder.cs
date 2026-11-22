using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Invoices.Common;

/// <summary>Nạp 1 hóa đơn (theo dõi thay đổi) và kiểm chi nhánh của hóa đơn nằm trong phạm vi quyền <paramref name="action"/> trên trang Hóa đơn.</summary>
public sealed class InvoiceFinder(IFinanceDbContext db, FinanceAccess access)
{
    public async Task<Result<Invoice>> FindAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var invoice = await db.Invoices.FirstOrDefaultAsync(i => i.Id == new InvoiceId(id), ct);
        if (invoice is null || !access.InBranch(FinanceAccess.Invoices, PermissionAction.View, invoice.BranchId.Value)) return InvoiceErrors.NotFound;
        if (!access.InBranch(FinanceAccess.Invoices, action, invoice.BranchId.Value)) return FinanceAccess.BranchNotAllowed;
        return invoice;
    }

    /// <summary>Người thao tác bắt buộc (thu / hoàn / hủy đều cần biết ai làm)</summary>
    public Result<Guid> Actor() => access.ActorId is { } id ? id : Error.Unauthorized("finance.actor_required", "Cần đăng nhập để thao tác");
}
