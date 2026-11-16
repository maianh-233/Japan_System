using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

public static class InvoiceErrors
{
    public static readonly Error Closed = Error.Conflict("finance.invoice.closed", "Hóa đơn đã hủy");
}
