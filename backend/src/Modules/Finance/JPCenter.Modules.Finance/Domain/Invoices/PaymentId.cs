using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Id lần thu tiền</summary>
public readonly record struct PaymentId(Guid Value)
{
    public static PaymentId New() => new(IdGenerator.NewId());
}
