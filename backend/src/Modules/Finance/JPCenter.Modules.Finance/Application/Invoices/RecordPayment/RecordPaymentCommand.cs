using FluentValidation;
using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Finance.Application.Abstractions;
using JPCenter.Modules.Finance.Application.Invoices.Common;
using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Invoices.RecordPayment;

/// <summary>
/// TC-06 — ghi 1 lần thu: số tiền ≤ còn nợ (E2), hình thức, thời điểm (mặc định bây giờ, không ở tương lai), ghi chú. Người thu = người đang gọi
/// (tiền mặt vào bàn giao ca — E5). Bấm 2 lần: endpoint bắt buộc Idempotency-Key (E4). Thu sai không xóa — dùng hoàn tiền (E6).
/// </summary>
public sealed record RecordPaymentCommand(Guid InvoiceId, decimal Amount, PaymentMethod Method, DateTimeOffset? PaidAt, string? Note)
    : ICommand<InvoiceDto>;

public sealed class RecordPaymentValidator : AbstractValidator<RecordPaymentCommand>
{
    public RecordPaymentValidator()
    {
        RuleFor(x => x.Method).IsInEnum();
        RuleFor(x => x.Note).MaximumLength(500);
    }
}

public sealed class RecordPaymentHandler(IFinanceDbContext db, InvoiceFinder finder, InvoiceReader reader, IClock clock)
    : ICommandHandler<RecordPaymentCommand, InvoiceDto>
{
    public static readonly Error FuturePayment = Error.Validation("finance.payment.future", "Thời điểm thu không được ở tương lai");

    public async Task<Result<InvoiceDto>> Handle(RecordPaymentCommand cmd, CancellationToken ct)
    {
        var actor = finder.Actor();
        if (actor.IsFailure) return actor.Error;
        var found = await finder.FindAsync(cmd.InvoiceId, PermissionAction.Update, ct);
        if (found.IsFailure) return found.Error;
        var money = Money.Create(cmd.Amount);
        if (money.IsFailure || cmd.Amount <= 0) return InvoiceErrors.InvalidAmount;
        var now = clock.UtcNow;
        if (cmd.PaidAt > now.AddMinutes(5)) return FuturePayment;

        var paid = found.Value.Pay(money.Value, cmd.Method, cmd.PaidAt ?? now, actor.Value, note: cmd.Note);
        if (paid.IsFailure) return paid.Error;
        found.Value.ClearDomainEvents();
        await db.SaveChangesAsync(ct);
        return await reader.ToDtoAsync(found.Value, ct);
    }
}
