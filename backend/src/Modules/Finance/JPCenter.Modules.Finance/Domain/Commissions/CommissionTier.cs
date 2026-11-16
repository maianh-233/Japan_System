using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Bậc hoa hồng Sale: doanh thu thực thu từ mức From → hưởng Percent %. Bảng: finance.commission_tiers</summary>
public sealed class CommissionTier : AggregateRoot<CommissionTierId>, IAuditable
{
    private CommissionTier() { }

    public Money From { get; private set; }
    public decimal Percent { get; private set; }

    public static Result<CommissionTier> Create(Money from, decimal percent)
    {
        if (percent is < 0 or > 100) return Error.Validation("finance.commission.invalid_percent", "Phần trăm từ 0 đến 100");
        return new CommissionTier { Id = CommissionTierId.New(), From = from, Percent = percent };
    }
}
