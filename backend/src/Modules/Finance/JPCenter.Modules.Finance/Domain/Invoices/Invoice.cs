using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices.Events;
using JPCenter.Modules.Finance.Domain.Pricing;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Hóa đơn học phí — sinh từ ghi danh. Thu nhiều đợt, hoàn tiền (qua duyệt 2 người khi vượt ngưỡng), hủy phần còn lại
/// (nghỉ / lớp hủy), hủy hóa đơn lập sai (duyệt 2 người). Bảng: finance.invoices (+ payments, refunds)
/// </summary>
public sealed class Invoice : AggregateRoot<InvoiceId>, IBranchScoped, IAuditable, ISoftDeletable
{
    private readonly List<Payment> _payments = [];
    private readonly List<Refund> _refunds = [];

    private Invoice() { }

    /// <summary>Mã theo mẫu ở Cài đặt, vd HD-CG-2611-0003</summary>
    public string Code { get; private set; } = null!;
    /// <summary>Students.Enrollment</summary>
    public Guid EnrollmentId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Học phí gốc</summary>
    public Money Amount { get; private set; }
    /// <summary>Số tiền giảm</summary>
    public Money Discount { get; private set; }
    public DiscountId? DiscountId { get; private set; }
    public InstallmentPlanId? InstallmentPlanId { get; private set; }
    public DateOnly DueDate { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    /// <summary>Nội dung chuyển khoản VietQR riêng của hóa đơn — dùng tự đối soát</summary>
    public string TransferContent { get; private set; } = null!;
    public InvoiceStatus Status { get; private set; }
    /// <summary>Hủy phần còn lại (nghỉ học / lớp hủy) → hết công nợ</summary>
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    /// <summary>Hủy hóa đơn lập sai — qua duyệt 2 người (System.ApprovalRequest)</summary>
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? VoidRequestedBy { get; private set; }
    public Guid? VoidApprovedBy { get; private set; }
    public IReadOnlyList<Payment> Payments => _payments;
    public IReadOnlyList<Refund> Refunds => _refunds;

    /// <summary>Phải thu = học phí − giảm</summary>
    public Money NetAmount => Amount - Discount;
    /// <summary>Thực thu = đã đóng − đã hoàn</summary>
    public Money PaidAmount => _payments.Aggregate(Money.Zero, (s, p) => s + p.Amount) - _refunds.Aggregate(Money.Zero, (s, r) => s + r.Amount);
    /// <summary>Còn nợ (0 khi đã hủy phần còn lại / hủy hóa đơn)</summary>
    public Money Balance => CancelledAt is not null || VoidedAt is not null ? Money.Zero : NetAmount - PaidAmount;

    public static Result<Invoice> Issue(string code, Guid enrollmentId, Guid studentId, Guid classId, BranchId branchId, Money amount,
        Money discount, DiscountId? discountId, InstallmentPlanId? planId, DateOnly dueDate, string transferContent, DateTimeOffset at)
    {
        if (discount > amount) return Error.Validation("finance.invoice.discount_too_large", "Số tiền giảm lớn hơn học phí");
        var inv = new Invoice
        {
            Id = InvoiceId.New(), Code = code, EnrollmentId = enrollmentId, StudentId = studentId, ClassId = classId, BranchId = branchId,
            Amount = amount, Discount = discount, DiscountId = discountId, InstallmentPlanId = planId, DueDate = dueDate,
            TransferContent = transferContent, IssuedAt = at, Status = InvoiceStatus.Unpaid,
        };
        inv.Raise(new InvoiceIssued(inv.Id, studentId, branchId, inv.NetAmount));
        return inv;
    }

    public Result<Payment> Pay(Money amount, PaymentMethod method, DateTimeOffset at, Guid collectorId, Guid? bankTransactionId, DateOnly today)
    {
        if (VoidedAt is not null || CancelledAt is not null) return InvoiceErrors.Closed;
        if (amount <= Money.Zero) return Error.Validation("finance.invoice.invalid_amount", "Số tiền phải lớn hơn 0");
        if (amount > Balance) return Error.Validation("finance.invoice.overpay", "Số tiền vượt quá số còn nợ");
        var p = new Payment(amount, method, at, collectorId, bankTransactionId);
        _payments.Add(p);
        RefreshStatus(today);
        Raise(new PaymentReceived(Id, p.Id, StudentId, BranchId, amount, collectorId, at));
        return p;
    }

    /// <param name="approvedBy">Người thứ 2 duyệt — bắt buộc khi số tiền ≥ ngưỡng ở Cài đặt (Application kiểm tra)</param>
    public Result<Refund> Refund(Money amount, PaymentMethod method, string reason, Guid by, Guid? approvedBy, DateTimeOffset at, DateOnly today)
    {
        if (VoidedAt is not null) return InvoiceErrors.Closed;
        if (amount <= Money.Zero || amount > PaidAmount) return Error.Validation("finance.invoice.invalid_refund", "Số tiền hoàn không hợp lệ");
        if (approvedBy == by) return Error.Forbidden("finance.invoice.self_approval", "Người tạo không tự duyệt hoàn tiền");
        var r = new Refund(amount, method, reason, by, approvedBy, at);
        _refunds.Add(r);
        RefreshStatus(today);
        return r;
    }

    public Result CancelRemaining(string reason, DateTimeOffset at)
    {
        if (VoidedAt is not null || CancelledAt is not null) return InvoiceErrors.Closed;
        (CancelledAt, CancelReason, Status) = (at, reason, InvoiceStatus.Cancelled);
        return Result.Success();
    }

    public Result Void(string reason, Guid requestedBy, Guid approvedBy, DateTimeOffset at)
    {
        if (VoidedAt is not null) return InvoiceErrors.Closed;
        if (requestedBy == approvedBy) return Error.Forbidden("finance.invoice.self_approval", "Người tạo không tự duyệt hủy hóa đơn");
        (VoidedAt, VoidReason, VoidRequestedBy, VoidApprovedBy, Status) = (at, reason, requestedBy, approvedBy, InvoiceStatus.Voided);
        return Result.Success();
    }

    /// <summary>Tính lại trạng thái (gọi sau mỗi thay đổi và job hằng ngày để đánh dấu quá hạn)</summary>
    public void RefreshStatus(DateOnly today)
    {
        if (VoidedAt is not null || CancelledAt is not null) return;
        Status = _refunds.Count > 0 && PaidAmount == Money.Zero ? InvoiceStatus.Refunded
            : Balance <= Money.Zero ? InvoiceStatus.Paid
            : today > DueDate ? InvoiceStatus.Overdue
            : PaidAmount > Money.Zero ? InvoiceStatus.Partial
            : InvoiceStatus.Unpaid;
    }
}
