using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Invoices.GetInvoice;

/// <summary>TC-05 bước 2 — chi tiết hóa đơn (kể cả đã hủy): các lần thu, hoàn, yêu cầu chờ duyệt, lịch sử nhắc. Ngoài phạm vi chi nhánh → 404.</summary>
public sealed record GetInvoiceQuery(Guid Id) : IQuery<InvoiceDto>;

public sealed class GetInvoiceHandler(InvoiceFinder finder, InvoiceReader reader) : IQueryHandler<GetInvoiceQuery, InvoiceDto>
{
    public async Task<Result<InvoiceDto>> Handle(GetInvoiceQuery q, CancellationToken ct)
    {
        var found = await finder.FindAsync(q.Id, PermissionAction.View, ct);
        return found.IsFailure ? found.Error : await reader.ToDtoAsync(found.Value, ct);
    }
}
