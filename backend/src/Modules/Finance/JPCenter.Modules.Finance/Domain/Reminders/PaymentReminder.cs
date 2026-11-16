using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>Lịch sử nhắc học phí của 1 hóa đơn (chỉ thêm). Bảng: finance.payment_reminders</summary>
public sealed class PaymentReminder : Entity<PaymentReminderId>
{
    private PaymentReminder() { }

    public InvoiceId InvoiceId { get; private set; }
    public ReminderChannel Channel { get; private set; }
    public DateTimeOffset At { get; private set; }
    /// <summary>null = hệ thống tự nhắc</summary>
    public Guid? By { get; private set; }
    public string? Note { get; private set; }

    public static PaymentReminder Create(InvoiceId invoiceId, ReminderChannel channel, Guid? by, string? note, DateTimeOffset at) =>
        new() { Id = PaymentReminderId.New(), InvoiceId = invoiceId, Channel = channel, By = by, Note = note, At = at };
}
