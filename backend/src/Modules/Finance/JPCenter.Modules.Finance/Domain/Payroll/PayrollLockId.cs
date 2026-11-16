using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Payroll;

/// <summary>Id bảng lương đã chốt</summary>
public readonly record struct PayrollLockId(Guid Value)
{
    public static PayrollLockId New() => new(IdGenerator.NewId());
}
