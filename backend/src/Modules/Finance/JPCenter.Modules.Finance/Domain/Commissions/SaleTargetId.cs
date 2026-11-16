using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Id chỉ tiêu Sale</summary>
public readonly record struct SaleTargetId(Guid Value)
{
    public static SaleTargetId New() => new(IdGenerator.NewId());
}
