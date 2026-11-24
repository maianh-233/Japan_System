using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Bảng bậc hoa hồng (TC-12): kiểm tra cả bảng, chọn bậc theo thực thu, tính hoa hồng. Khớp <c>financeUtils.ts › tierOf / saleResults</c>.</summary>
public static class CommissionTable
{
    /// <summary>Dựng bảng mới thay toàn bộ bảng cũ — bắt buộc có mốc 0đ, không trùng mốc, % trong 0 → 100 (TC-12.E1, E2). Xếp mốc tăng dần.</summary>
    public static Result<List<CommissionTier>> Build(IEnumerable<(Money From, decimal Percent)> tiers)
    {
        var list = tiers.ToList();
        if (list.Any(t => t.Percent is < 0 or > 100)) return CommissionErrors.InvalidPercent;
        if (!list.Any(t => t.From == Money.Zero)) return CommissionErrors.ZeroTierRequired;
        if (list.Select(t => t.From).Distinct().Count() != list.Count) return CommissionErrors.DuplicateTier;
        return list.OrderBy(t => t.From).Select(t => CommissionTier.Create(t.From, t.Percent).Value).ToList();
    }

    /// <summary>Bậc cao nhất có mốc ≤ thực thu; thực thu âm (hoàn nhiều hơn thu trong tháng) → không bậc nào</summary>
    public static CommissionTier? TierOf(IEnumerable<CommissionTier> tiers, decimal collected) =>
        tiers.OrderByDescending(t => t.From).FirstOrDefault(t => collected >= t.From.Amount);

    /// <summary>Hoa hồng = thực thu × % bậc, làm tròn về đồng; âm → 0</summary>
    public static Money CommissionOf(IEnumerable<CommissionTier> tiers, decimal collected)
    {
        if (collected <= 0 || TierOf(tiers, collected) is not { } tier) return Money.Zero;
        return Money.FromTrusted(decimal.Round(collected * tier.Percent / 100m, 0, MidpointRounding.AwayFromZero));
    }
}
