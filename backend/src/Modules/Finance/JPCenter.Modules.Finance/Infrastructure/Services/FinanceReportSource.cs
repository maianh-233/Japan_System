using JPCenter.Application.Common.Time;
using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Contracts.Dtos.Reports;
using JPCenter.Modules.Finance.Domain.BankTransactions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="IFinanceReportSource"/> — trạng thái tiền tính khi đọc (<c>Invoice.Summarize</c>, <see cref="DebtAgingRule"/>) nên đọc hóa đơn
/// lên bộ nhớ rồi gom, giống trang Công nợ.
/// </summary>
internal sealed class FinanceReportSource(FinanceDbContext db, IClock clock) : IFinanceReportSource
{
    public async Task<IReadOnlyList<CashMonthFact>> CashByMonthAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, CancellationToken ct)
    {
        var start = TimeText.At(new DateOnly(from.Year, from.Month, 1), TimeOnly.MinValue);
        var invoices = await Invoices(branchIds)
            .Where(i => i.Payments.Any(p => p.PaidAt >= start) || i.Refunds.Any(r => r.RefundedAt >= start))
            .ToListAsync(ct);
        var moves = invoices.SelectMany(i =>
            i.Payments.Where(p => p.PaidAt >= start).Select(p => (i.BranchId, At: p.PaidAt, p.Amount.Amount))
                .Concat(i.Refunds.Where(r => r.RefundedAt >= start).Select(r => (i.BranchId, At: r.RefundedAt, Amount: -r.Amount.Amount))));
        return moves
            .GroupBy(m => (m.BranchId, Month: MonthOf(TimeText.DateOf(m.At))))
            .Select(g => new CashMonthFact(g.Key.BranchId.Value, g.Key.Month, g.Sum(m => m.Amount)))
            .OrderBy(f => f.Month).ToList();
    }

    public async Task<IReadOnlyList<BranchAmount>> BookedAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, CancellationToken ct)
    {
        var start = TimeText.At(from, TimeOnly.MinValue);
        var invoices = await Invoices(branchIds).Where(i => i.Status == InvoiceStatus.Open && i.IssuedAt >= start).ToListAsync(ct);
        return invoices.GroupBy(i => i.BranchId).Select(g => new BranchAmount(g.Key.Value, g.Sum(i => i.Total.Amount))).ToList();
    }

    public async Task<IReadOnlyList<DebtFact>> DebtsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var today = clock.Today;
        var open = await Invoices(branchIds).Where(i => i.Status == InvoiceStatus.Open).ToListAsync(ct);
        return open.Select(i => (Invoice: i, Summary: i.Summarize(today)))
            .Where(x => x.Summary.Remaining > Money.Zero)
            .Select(x =>
            {
                var due = x.Summary.CurrentDueDate ?? x.Invoice.DueDate;
                return new DebtFact(x.Invoice.Id.Value, x.Invoice.Code, x.Invoice.BranchId.Value, x.Invoice.StudentId, x.Invoice.ClassId, due,
                    x.Summary.Remaining.Amount, SnakeCaseEnumConverter<DebtAging>.ToText(DebtAgingRule.Of(due, today)),
                    x.Summary.Status == TuitionStatus.Overdue);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<UnmatchedTransactionFact>> UnmatchedTransactionsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var query = db.BankTransactions.AsNoTracking().Where(t => t.Status == TransactionStatus.Unmatched);
        if (branchIds is not null)
        {
            var ids = branchIds.Select(b => new BranchId(b)).ToList();
            query = query.Where(t => ids.Contains(t.BranchId));
        }
        var rows = await query.OrderBy(t => t.At).ToListAsync(ct);
        return rows.Select(t => new UnmatchedTransactionFact(t.Id.Value, t.BranchId.Value, t.At, t.Amount.Amount, t.Payer, t.Content)).ToList();
    }

    private IQueryable<Invoice> Invoices(IReadOnlyCollection<Guid>? branchIds)
    {
        var query = db.Invoices.AsNoTracking();
        if (branchIds is null) return query;
        var ids = branchIds.Select(b => new BranchId(b)).ToList();
        return query.Where(i => ids.Contains(i.BranchId));
    }

    private static DateOnly MonthOf(DateOnly day) => new(day.Year, day.Month, 1);
}
