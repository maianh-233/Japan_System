using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>1 lần thu tiền của hóa đơn. Bảng: finance.payments</summary>
public sealed class Payment : Entity<PaymentId>
{
    private Payment() { }

    internal Payment(Money amount, PaymentMethod method, DateTimeOffset paidAt, Guid collectorId, Guid? bankTransactionId) : base(PaymentId.New()) =>
        (Amount, Method, PaidAt, CollectorId, BankTransactionId) = (amount, method, paidAt, collectorId, bankTransactionId);

    public Money Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset PaidAt { get; private set; }
    /// <summary>Người thu (Hr.Employee) — tiền mặt tính vào bàn giao ca</summary>
    public Guid CollectorId { get; private set; }
    /// <summary>Giao dịch ngân hàng đã khớp (VietQR / chuyển khoản)</summary>
    public Guid? BankTransactionId { get; private set; }
}
