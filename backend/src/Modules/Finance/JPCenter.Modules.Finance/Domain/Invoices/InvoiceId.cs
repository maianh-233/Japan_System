using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Id hóa đơn</summary>
public readonly record struct InvoiceId(Guid Value)
{
    public static InvoiceId New() => new(IdGenerator.NewId());
}
