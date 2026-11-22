using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.Common;

/// <summary>Nạp 1 giao dịch (theo dõi thay đổi) trong phạm vi chi nhánh của trang Đối soát</summary>
public sealed class TransactionFinder(IFinanceDbContext db, FinanceAccess access)
{
    public async Task<Result<BankTransaction>> FindAsync(Guid id, PermissionAction action, CancellationToken ct)
    {
        var txn = await db.BankTransactions.FirstOrDefaultAsync(t => t.Id == new BankTransactionId(id), ct);
        if (txn is null || !access.InBranch(FinanceAccess.Reconciliation, PermissionAction.View, txn.BranchId.Value)) return BankTransactionErrors.NotFound;
        if (!access.InBranch(FinanceAccess.Reconciliation, action, txn.BranchId.Value)) return FinanceAccess.BranchNotAllowed;
        return txn;
    }
}
