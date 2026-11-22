using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.UnmatchTransaction;

/// <summary>TC-10 bước 4 — bỏ khớp (khớp nhầm): xóa các khoản thu của giao dịch trên mọi hóa đơn đã khớp, giao dịch về "chưa khớp"</summary>
public sealed record UnmatchTransactionCommand(Guid TransactionId) : ICommand<BankTxnDto>;

public sealed class UnmatchTransactionHandler(IFinanceDbContext db, TransactionFinder finder, BankMatcher matcher)
    : ICommandHandler<UnmatchTransactionCommand, BankTxnDto>
{
    public async Task<Result<BankTxnDto>> Handle(UnmatchTransactionCommand cmd, CancellationToken ct)
    {
        var found = await finder.FindAsync(cmd.TransactionId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var txn = found.Value;
        var released = txn.Unmatch();
        if (released.IsFailure) return released.Error;
        var ids = released.Value.ToList();
        var invoices = await db.Invoices.IgnoreQueryFilters().Where(i => ids.Contains(i.Id)).ToListAsync(ct);
        // Khoản thu đã bị bù trừ trước đó (vd hóa đơn đã hủy) thì không còn — bỏ qua
        foreach (var invoice in invoices) invoice.RemoveBankPayments(txn.Id.Value);
        await db.SaveChangesAsync(ct);
        return await matcher.ToDtoAsync(txn, ct);
    }
}
