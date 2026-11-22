using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.GetBankTransactions;

/// <summary>TC-10 — giao dịch ngân hàng trong phạm vi chi nhánh, lọc trạng thái / chi nhánh; mới nhất trước</summary>
public sealed record GetBankTransactionsQuery(TransactionStatus? Status, Guid? BranchId) : IQuery<IReadOnlyList<BankTxnDto>>;

public sealed class GetBankTransactionsHandler(IFinanceDbContext db, FinanceAccess access, BankMatcher matcher)
    : IQueryHandler<GetBankTransactionsQuery, IReadOnlyList<BankTxnDto>>
{
    public async Task<Result<IReadOnlyList<BankTxnDto>>> Handle(GetBankTransactionsQuery q, CancellationToken ct)
    {
        var query = db.BankTransactions.AsNoTracking();
        if (access.Branches(FinanceAccess.Reconciliation, PermissionAction.View) is { } allowed)
        {
            var ids = allowed.Select(b => new BranchId(b)).ToList();
            query = query.Where(t => ids.Contains(t.BranchId));
        }
        if (q.Status is { } status) query = query.Where(t => t.Status == status);
        if (q.BranchId is { } branch) query = query.Where(t => t.BranchId == new BranchId(branch));
        return Result.Success(await matcher.ToDtosAsync(await query.OrderByDescending(t => t.At).ToListAsync(ct), ct));
    }
}
