using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>
/// Bậc hoa hồng Sale: doanh thu thực thu từ mức From → hưởng Percent % trên CẢ số thực thu (không lũy tiến).
/// Sửa cả bảng 1 lần qua <see cref="CommissionTable.Build"/>. Bảng: finance.commission_tiers
/// </summary>
public sealed class CommissionTier : AggregateRoot<CommissionTierId>, IAuditable
{
    private CommissionTier() { }

    public Money From { get; private set; }
    public decimal Percent { get; private set; }

    public static Result<CommissionTier> Create(Money from, decimal percent)
    {
        if (percent is < 0 or > 100) return CommissionErrors.InvalidPercent;
        return new CommissionTier { Id = CommissionTierId.New(), From = from, Percent = percent };
    }
}
