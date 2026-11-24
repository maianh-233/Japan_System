using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Domain.Commissions;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Payroll;
using JPCenter.Modules.Hr.Contracts;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Finance.Application.Commissions.Common;

/// <summary>
/// TC-12 — kết quả tháng của mọi Sale (phòng <c>sales</c>, còn làm trong tháng): hóa đơn ghi nhận theo <c>invoices.sale_id</c>, thực thu = thu − hoàn
/// có thời điểm trong tháng theo giờ VN → bậc → hoa hồng (<see cref="SalePerformance"/>). Dùng cho trang Hoa hồng và lương NV (TC-14).
/// </summary>
public sealed class CommissionCalculator(IFinanceDbContext db, IPayrollSource hr)
{
    public const string SalesDepartment = "sales";

    public async Task<IReadOnlyList<CommissionTier>> TiersAsync(CancellationToken ct) =>
        await db.CommissionTiers.AsNoTracking().OrderBy(t => t.From).ToListAsync(ct);

    public async Task<IReadOnlyList<SaleResultDto>> ResultsAsync(string month, CancellationToken ct)
    {
        var (first, last) = PayrollMonth.Range(month);
        var (from, to) = (VnTime.StartOf(first), VnTime.StartOf(last.AddDays(1)));
        var tiers = await TiersAsync(ct);
        var sales = (await hr.ProfilesAsync(first, last, ct)).Where(p => p.Department == SalesDepartment).ToList();
        var ids = sales.Select(s => (Guid?)s.Id).ToList();

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.SaleId) && i.Status != InvoiceStatus.Voided
                && ((i.IssuedAt >= from && i.IssuedAt < to)
                    || i.Payments.Any(p => p.PaidAt >= from && p.PaidAt < to)
                    || i.Refunds.Any(r => r.RefundedAt >= from && r.RefundedAt < to)))
            .ToListAsync(ct);
        var targets = await db.SaleTargets.AsNoTracking().Where(t => t.Month == month).ToDictionaryAsync(t => t.EmployeeId, t => t.Target, ct);

        var results = sales.Select(s =>
        {
            var mine = invoices.Where(i => i.SaleId == s.Id).ToList();
            var deals = mine.Where(i => i.IssuedAt >= from && i.IssuedAt < to).ToList();
            var collected = mine.Sum(i => i.Payments.Where(p => p.PaidAt >= from && p.PaidAt < to).Sum(p => p.Amount.Amount))
                - mine.Sum(i => i.Refunds.Where(r => r.RefundedAt >= from && r.RefundedAt < to).Sum(r => r.Amount.Amount));
            var perf = SalePerformance.Of(s.Id, deals.Count, Money.FromTrusted(deals.Sum(d => d.Total.Amount)), collected,
                targets.GetValueOrDefault(s.Id, Money.Zero), tiers);
            return (Profile: s, Deals: deals.Select(d => d.Id.Value).ToList(), Perf: perf);
        }).OrderByDescending(x => x.Perf.Collected).ThenBy(x => x.Profile.FullName).ToList();

        return results.Select((x, i) => new SaleResultDto(x.Profile.Id, x.Profile.Code, x.Profile.FullName, x.Profile.BranchId, x.Deals,
            x.Perf.Booked.Amount, x.Perf.Collected, x.Perf.Target.Amount, x.Perf.Kpi,
            x.Perf.Tier is { } t ? new CommissionTierDto(t.From.Amount, t.Percent) : null, x.Perf.Commission.Amount, i + 1)).ToList();
    }
}
