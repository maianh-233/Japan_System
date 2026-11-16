using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>1 lần hoàn tiền (HV nghỉ / lớp hủy). Bảng: finance.refunds</summary>
public sealed class Refund : Entity<RefundId>
{
    private Refund() { }

    internal Refund(Money amount, PaymentMethod method, string reason, Guid by, Guid? approvedBy, DateTimeOffset refundedAt) : base(RefundId.New()) =>
        (Amount, Method, Reason, By, ApprovedBy, RefundedAt) = (amount, method, reason, by, approvedBy, refundedAt);

    public Money Amount { get; private set; }
    public PaymentMethod Method { get; private set; }
    public DateTimeOffset RefundedAt { get; private set; }
    public string Reason { get; private set; } = null!;
    public Guid By { get; private set; }
    /// <summary>Người thứ 2 duyệt</summary>
    public Guid? ApprovedBy { get; private set; }
}
