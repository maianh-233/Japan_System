using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Pricing;

/// <summary>Id chính sách đóng nhiều đợt</summary>
public readonly record struct InstallmentPlanId(Guid Value)
{
    public static InstallmentPlanId New() => new(IdGenerator.NewId());
}
