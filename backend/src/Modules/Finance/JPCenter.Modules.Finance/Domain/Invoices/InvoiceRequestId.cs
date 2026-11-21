using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Id yêu cầu duyệt trên hóa đơn</summary>
public readonly record struct InvoiceRequestId(Guid Value)
{
    public static InvoiceRequestId New() => new(IdGenerator.NewId());
}
