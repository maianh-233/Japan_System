using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Reconciliation.Common;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Reconciliation.MatchTransaction;

/// <summary>
/// TC-10 bước 4 — khớp tay giao dịch vào 1 hóa đơn (số tiền bỏ trống = cả giao dịch) hoặc tách vào nhiều hóa đơn (tổng = số tiền giao dịch — E3).
/// Mỗi phần ghi 1 khoản thu VietQR gắn giao dịch; vượt còn nợ → <c>finance.invoice.overpay</c>. Đã khớp → E2.
/// </summary>
public sealed record MatchTransactionCommand(Guid TransactionId, IReadOnlyList<MatchAllocation> Allocations) : ICommand<BankTxnDto>;

/// <param name="Amount">null = toàn bộ số tiền giao dịch (chỉ khi khớp 1 hóa đơn)</param>
public sealed record MatchAllocation(Guid InvoiceId, decimal? Amount);

public sealed class MatchTransactionHandler(IFinanceDbContext db, TransactionFinder finder, FinanceAccess access, BankMatcher matcher, IClock clock)
    : ICommandHandler<MatchTransactionCommand, BankTxnDto>
{
    public async Task<Result<BankTxnDto>> Handle(MatchTransactionCommand cmd, CancellationToken ct)
    {
        if (access.ActorId is not { } actor) return FinanceAccess.BranchNotAllowed;
        var found = await finder.FindAsync(cmd.TransactionId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var txn = found.Value;
        if (txn.Status is TransactionStatus.Matched or TransactionStatus.Auto) return BankTransactionErrors.AlreadyMatched;
        if (cmd.Allocations.Count == 0 || cmd.Allocations.Select(a => a.InvoiceId).Distinct().Count() != cmd.Allocations.Count)
            return BankTransactionErrors.InvoicesRequired;

        var parts = cmd.Allocations.Count == 1 && cmd.Allocations[0].Amount is null
            ? [(cmd.Allocations[0].InvoiceId, txn.Amount.Amount)]
            : cmd.Allocations.Select(a => (a.InvoiceId, a.Amount ?? 0)).ToList();
        if (parts.Any(p => p.Item2 <= 0 || decimal.Truncate(p.Item2) != p.Item2) || parts.Sum(p => p.Item2) != txn.Amount.Amount)
            return BankTransactionErrors.AllocationMismatch;

        var ids = parts.Select(p => new InvoiceId(p.InvoiceId)).ToList();
        var invoices = await db.Invoices.Where(i => ids.Contains(i.Id)).ToDictionaryAsync(i => i.Id, ct);
        foreach (var (invoiceId, amount) in parts)
        {
            if (!invoices.TryGetValue(new InvoiceId(invoiceId), out var invoice)) return InvoiceErrors.NotFound;
            var paid = invoice.Pay(Money.FromTrusted(amount), PaymentMethod.Vietqr, txn.At, actor, txn.Id.Value, $"Khớp giao dịch {txn.Ref}");
            if (paid.IsFailure) return paid.Error;
            invoice.ClearDomainEvents();
        }
        if (txn.Match(ids, actor, clock.UtcNow) is { IsFailure: true } f) return f.Error;
        await db.SaveChangesAsync(ct);
        return await matcher.ToDtoAsync(txn, ct);
    }
}
