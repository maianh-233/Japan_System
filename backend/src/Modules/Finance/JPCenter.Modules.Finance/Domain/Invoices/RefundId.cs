using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Id lần hoàn tiền</summary>
public readonly record struct RefundId(Guid Value)
{
    public static RefundId New() => new(IdGenerator.NewId());
}
