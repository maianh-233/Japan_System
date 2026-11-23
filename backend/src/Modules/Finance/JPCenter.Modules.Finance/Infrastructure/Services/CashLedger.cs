using JPCenter.Modules.Finance.Contracts;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Infrastructure.Services;

/// <summary>
/// Hiện thực <see cref="ICashLedger"/> — tiền mặt thu trong khung giờ (phiếu thu <c>method = cash</c>) trừ tiền mặt hoàn trong cùng khung.
/// Tính cả hóa đơn đã hủy / vô hiệu: tiền đã thu thật vào quầy thì vẫn nằm trong két.
/// </summary>
internal sealed class CashLedger(FinanceDbContext db) : ICashLedger
{
    public async Task<decimal> CashCollectedAsync(Guid branchId, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var branch = new BranchId(branchId);
        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.BranchId == branch
                && (i.Payments.Any(p => p.Method == PaymentMethod.Cash && p.PaidAt >= from && p.PaidAt < to)
                    || i.Refunds.Any(r => r.Method == PaymentMethod.Cash && r.RefundedAt >= from && r.RefundedAt < to)))
            .ToListAsync(ct);
        var paid = invoices.SelectMany(i => i.Payments)
            .Where(p => p.Method == PaymentMethod.Cash && p.PaidAt >= from && p.PaidAt < to).Sum(p => p.Amount.Amount);
        var refunded = invoices.SelectMany(i => i.Refunds)
            .Where(r => r.Method == PaymentMethod.Cash && r.RefundedAt >= from && r.RefundedAt < to).Sum(r => r.Amount.Amount);
        return paid - refunded;
    }
}
