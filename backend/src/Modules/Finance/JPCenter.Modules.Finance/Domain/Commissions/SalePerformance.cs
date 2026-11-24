using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Kết quả tháng của 1 Sale (TC-12 bước 3)</summary>
/// <param name="Booked">Doanh số ký = Σ phải thu hóa đơn lập trong tháng</param>
/// <param name="Collected">Thực thu = Σ thu − Σ hoàn trong tháng (có thể âm khi hoàn hóa đơn tháng trước — TC-12.E3)</param>
/// <param name="Kpi">Thực thu ÷ chỉ tiêu (0 khi chưa đặt chỉ tiêu)</param>
public sealed record SalePerformance(Guid SaleId, int Deals, Money Booked, decimal Collected, Money Target, decimal Kpi, CommissionTier? Tier,
    Money Commission)
{
    /// <summary>Tính từ số liệu đã gom theo Sale; xếp hạng = sắp theo <see cref="Collected"/> giảm dần</summary>
    public static SalePerformance Of(Guid saleId, int deals, Money booked, decimal collected, Money target, IReadOnlyCollection<CommissionTier> tiers) =>
        new(saleId, deals, booked, collected, target, target > Money.Zero ? decimal.Round(collected / target.Amount, 4) : 0,
            collected > 0 ? CommissionTable.TierOf(tiers, collected) : null, CommissionTable.CommissionOf(tiers, collected));
}
