using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Id ưu đãi</summary>
public readonly record struct DiscountId(Guid Value)
{
    public static DiscountId New() => new(IdGenerator.NewId());
}
