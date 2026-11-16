using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Commissions;

/// <summary>Id bậc hoa hồng</summary>
public readonly record struct CommissionTierId(Guid Value)
{
    public static CommissionTierId New() => new(IdGenerator.NewId());
}
