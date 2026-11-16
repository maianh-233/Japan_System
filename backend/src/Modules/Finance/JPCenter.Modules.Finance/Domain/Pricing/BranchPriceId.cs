using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Id giá theo chi nhánh</summary>
public readonly record struct BranchPriceId(Guid Value)
{
    public static BranchPriceId New() => new(IdGenerator.NewId());
}
