using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Invoices.RequestVoid;

/// <summary>
/// TC-08 — đề nghị hủy hóa đơn lập sai (lý do ≥ 5 ký tự), luôn qua người thứ 2 duyệt ở hộp duyệt chung. Còn tiền đã thu chưa hoàn → chặn (E1);
/// đã có yêu cầu hủy đang chờ → chặn. Ghi danh của hóa đơn không tự đổi (E5).
/// </summary>
public sealed record RequestVoidCommand(Guid InvoiceId, string? Reason) : ICommand<InvoiceActionResult>;

public sealed class RequestVoidValidator : AbstractValidator<RequestVoidCommand>
{
    public RequestVoidValidator() => RuleFor(x => x.Reason).MaximumLength(500);
}

public sealed class RequestVoidHandler(IFinanceDbContext db, InvoiceFinder finder, InvoiceReader reader, IApprovalInbox inbox, IClock clock)
    : ICommandHandler<RequestVoidCommand, InvoiceActionResult>
{
    public async Task<Result<InvoiceActionResult>> Handle(RequestVoidCommand cmd, CancellationToken ct)
    {
        var actor = finder.Actor();
        if (actor.IsFailure) return actor.Error;
        var found = await finder.FindAsync(cmd.InvoiceId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var invoice = found.Value;
        var requested = invoice.RequestVoid(cmd.Reason, actor.Value, clock.UtcNow);
        if (requested.IsFailure) return requested.Error;

        var request = requested.Value;
        var ticket = await inbox.OpenAsync(new("void_invoice", $"Hủy hóa đơn {invoice.Code}", request.Reason, invoice.BranchId.Value,
            new { kind = "void_invoice", invoiceId = invoice.Id.Value, invoiceCode = invoice.Code, reason = request.Reason }), ct);
        invoice.LinkApproval(request.Id, ticket.Id);
        await db.SaveChangesAsync(ct);
        return new InvoiceActionResult(await reader.ToDtoAsync(invoice, ct), new ApprovalRef(ticket.Id, ticket.Code));
    }
}
