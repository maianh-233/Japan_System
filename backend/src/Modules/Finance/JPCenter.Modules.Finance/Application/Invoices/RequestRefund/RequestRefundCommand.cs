using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Common;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.System.Contracts;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Invoices.RequestRefund;

/// <summary>
/// TC-07 — hoàn tiền: ≤ hoàn tối đa (đã trừ khoản đang chờ — E1), có lý do. Số tiền &lt; ngưỡng <c>safety.refundApprovalMin</c> → ghi hoàn ngay;
/// từ ngưỡng → mở phiếu ở hộp duyệt chung (System), người thứ 2 duyệt mới ghi hoàn (E2 người tạo không tự duyệt).
/// </summary>
public sealed record RequestRefundCommand(Guid InvoiceId, decimal Amount, PaymentMethod Method, string? Reason) : ICommand<InvoiceActionResult>;

public sealed class RequestRefundValidator : AbstractValidator<RequestRefundCommand>
{
    public RequestRefundValidator()
    {
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Reason).MaximumLength(500);
    }
}

public sealed class RequestRefundHandler(
    IFinanceDbContext db, InvoiceFinder finder, InvoiceReader reader, ICenterSettings settings, IApprovalInbox inbox, IClock clock)
    : ICommandHandler<RequestRefundCommand, InvoiceActionResult>
{
    public async Task<Result<InvoiceActionResult>> Handle(RequestRefundCommand cmd, CancellationToken ct)
    {
        var actor = finder.Actor();
        if (actor.IsFailure) return actor.Error;
        var found = await finder.FindAsync(cmd.InvoiceId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var invoice = found.Value;
        var money = Money.Create(cmd.Amount);
        if (money.IsFailure) return InvoiceErrors.InvalidRefund;

        var needsApproval = cmd.Amount >= (await settings.GetAsync(ct)).RefundApprovalMin;
        var outcome = invoice.RequestRefund(money.Value, cmd.Method, cmd.Reason, actor.Value, needsApproval, clock.UtcNow);
        if (outcome.IsFailure) return outcome.Error;

        ApprovalRef? approval = null;
        if (outcome.Value.Request is { } request)
        {
            // Phiếu System lưu trước; Finance lưu sau khi gắn phiếu (lỗi lưu ở đây → phiếu mồ côi, khi duyệt báo "không tìm thấy yêu cầu")
            var ticket = await inbox.OpenAsync(new("refund", $"Hoàn {MoneyText.Vnd(cmd.Amount)} — {invoice.Code}", request.Reason, invoice.BranchId.Value,
                new { kind = "refund", invoiceId = invoice.Id.Value, invoiceCode = invoice.Code, amount = cmd.Amount, method = InvoiceReader.MethodText(cmd.Method),
                    reason = request.Reason }), ct);
            invoice.LinkApproval(request.Id, ticket.Id);
            approval = new ApprovalRef(ticket.Id, ticket.Code);
        }
        invoice.ClearDomainEvents();
        await db.SaveChangesAsync(ct);
        return new InvoiceActionResult(await reader.ToDtoAsync(invoice, ct), approval);
    }
}
