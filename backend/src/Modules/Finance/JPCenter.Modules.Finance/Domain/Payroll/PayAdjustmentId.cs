using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Id điều chỉnh lương</summary>
public readonly record struct PayAdjustmentId(Guid Value)
{
    public static PayAdjustmentId New() => new(IdGenerator.NewId());
}
