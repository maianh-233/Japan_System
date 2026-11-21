using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Id bộ đếm số hóa đơn</summary>
public readonly record struct InvoiceCounterId(Guid Value)
{
    public static InvoiceCounterId New() => new(IdGenerator.NewId());
}
