using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>1 lần thu tiền của hóa đơn. Không xóa — thu sai thì hoàn (TC-06.E6); chỉ "bỏ khớp" giao dịch VietQR xóa được. Bảng: finance.payments</summary>
public sealed class Payment : Entity<PaymentId>
{
    private Payment() { }

    internal Payment(Money amount, PaymentMethod method, DateTimeOffset paidAt, Guid? collectorId, Guid? bankTransactionId, string? note)
        : base(PaymentId.New()) =>
        (Amount, Method, PaidAt, CollectorId, BankTransactionId, Note) = (amount, method, paidAt, collectorId, bankTransactionId, note);

    public Money Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    /// <summary>Người thu (Hr.Employee) — tiền mặt tính vào bàn giao ca; null = hệ thống tự khớp giao dịch ngân hàng</summary>
    public Guid? CollectorId { get; private set; }
    /// <summary>Giao dịch ngân hàng đã khớp (VietQR / chuyển khoản)</summary>
    public Guid? BankTransactionId { get; private set; }
    public string? Note { get; private set; }
}
