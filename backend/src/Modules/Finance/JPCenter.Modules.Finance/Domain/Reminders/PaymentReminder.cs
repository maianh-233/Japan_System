using JPCenter.SharedKernel.Domain;
using JPCenter.Modules.Finance.Domain.Invoices;

namespace JPCenter.Modules.Finance.Domain.Reminders;

/// <summary>
/// Lịch sử nhắc học phí của 1 hóa đơn (chỉ thêm — TC-11). Tự nhắc ghi <see cref="Milestone"/> để mỗi mốc chỉ nhắc 1 lần.
/// Bảng: finance.payment_reminders
/// </summary>
public sealed class PaymentReminder : Entity<PaymentReminderId>
{
    private PaymentReminder() { }

    public InvoiceId InvoiceId { get; private set; }
    public ReminderChannel Channel { get; private set; }
    public DateTimeOffset At { get; private set; }
    /// <summary>null = hệ thống tự nhắc</summary>
    public Guid? By { get; private set; }
    public string? Note { get; private set; }
    /// <summary>Mốc tự nhắc (null = nhắc tay)</summary>
    public ReminderMilestone? Milestone { get; private set; }

    /// <summary>Nhắc tay (kế toán / giáo vụ chọn nhiều hóa đơn — TC-11 bước 3)</summary>
    public static PaymentReminder Manual(InvoiceId invoiceId, ReminderChannel channel, Guid by, string? note, DateTimeOffset at) =>
        new() { Id = PaymentReminderId.New(), InvoiceId = invoiceId, Channel = channel, By = by, Note = Clean(note), At = at };

    /// <summary>Hệ thống tự nhắc theo mốc (TC-11 bước 4)</summary>
    public static PaymentReminder Auto(InvoiceId invoiceId, ReminderChannel channel, ReminderMilestone milestone, DateTimeOffset at) =>
        new() { Id = PaymentReminderId.New(), InvoiceId = invoiceId, Channel = channel, Milestone = milestone, At = at };

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
