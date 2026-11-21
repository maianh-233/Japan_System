using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using JPCenter.Modules.Finance.Domain.Invoices.Events;
using JPCenter.Modules.Finance.Domain.Pricing;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Hóa đơn học phí — CHỈ sinh từ ghi danh (quy tắc vàng 2). Thu nhiều đợt, hoàn tiền (từ ngưỡng qua duyệt 2 người), hủy phần còn lại
/// (nghỉ / chuyển lớp / lớp hủy), hủy hóa đơn lập sai (luôn duyệt 2 người). Đã thu / còn nợ / tình trạng KHÔNG lưu — tính từ
/// <see cref="Payments"/>, <see cref="Refunds"/> (<see cref="Summarize"/>). Bảng: finance.invoices (+ payments, refunds, invoice_requests)
/// </summary>
public sealed class Invoice : AggregateRoot<InvoiceId>, IBranchScoped, IAuditable, ISoftDeletable
{
    public const int MinVoidReason = 5;

    private readonly List<Payment> _payments = [];
    private readonly List<Refund> _refunds = [];
    private readonly List<InvoiceRequest> _requests = [];

    private Invoice() { }

    /// <summary>Mã theo mẫu ở Cài đặt, vd HD-CG-2611-0003 — không tái sử dụng (quy tắc vàng 4)</summary>
    public string Code { get; private set; } = null!;
    /// <summary>Students.Enrollment — mỗi ghi danh tối đa 1 hóa đơn (TC-04.E1)</summary>
    public Guid EnrollmentId { get; private set; }
    /// <summary>Students.Student</summary>
    public Guid StudentId { get; private set; }
    /// <summary>Academic.Class</summary>
    public Guid ClassId { get; private set; }
    public BranchId BranchId { get; private set; }
    /// <summary>Học phí gốc (đã cộng phụ thu đóng nhiều đợt nếu có)</summary>
    public Money Amount { get; private set; }
    /// <summary>Số tiền giảm (tay / mã ưu đãi / khấu trừ chuyển lớp)</summary>
    public Money Discount { get; private set; }
    public DiscountId? DiscountId { get; private set; }
    public InstallmentPlanId? InstallmentPlanId { get; private set; }
    /// <summary>Đóng nhiều đợt: số tiền + hạn từng đợt (rỗng = 1 lần, hạn <see cref="DueDate"/>)</summary>
    public List<InstallmentDue> Schedule { get; private set; } = [];
    /// <summary>Hạn đóng (đóng nhiều đợt: hạn đợt cuối)</summary>
    public DateOnly DueDate { get; private set; }
    public DateTimeOffset IssuedAt { get; private set; }
    /// <summary>Nội dung chuyển khoản VietQR riêng của hóa đơn — dùng tự đối soát</summary>
    public string TransferContent { get; private set; } = null!;
    public InvoiceStatus Status { get; private set; }
    public DateTimeOffset? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public DateTimeOffset? VoidedAt { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? VoidRequestedBy { get; private set; }
    public Guid? VoidApprovedBy { get; private set; }
    public IReadOnlyList<Payment> Payments => _payments;
    public IReadOnlyList<Refund> Refunds => _refunds;
    public IReadOnlyList<InvoiceRequest> Requests => _requests;

    /// <summary>Phải thu = học phí − giảm</summary>
    public Money Total => Amount - Discount;
    public Money Collected => Sum(_payments.Select(p => p.Amount));
    public Money RefundedAmount => Sum(_refunds.Select(r => r.Amount));
    /// <summary>Thực thu = đã thu − đã hoàn</summary>
    public Money PaidAmount => Collected - RefundedAmount;
    /// <summary>Còn nợ = phải thu − đã thu; 0 khi đã hủy phần còn lại / hủy hóa đơn</summary>
    public Money Remaining => Status != InvoiceStatus.Open || Collected >= Total ? Money.Zero : Total - Collected;
    /// <summary>Khoản hoàn đang chờ duyệt</summary>
    public Money PendingRefunds => Sum(_requests.Where(r => r.IsPending && r.Kind == InvoiceRequestKind.Refund).Select(r => r.Amount ?? Money.Zero));
    /// <summary>Hoàn tối đa = thực thu − khoản hoàn đang chờ duyệt (TC-05.E2)</summary>
    public Money Refundable => Status == InvoiceStatus.Voided || PaidAmount <= PendingRefunds ? Money.Zero : PaidAmount - PendingRefunds;

    /// <summary>
    /// TC-04 — lập hóa đơn cho 1 ghi danh. Giảm ≤ học phí (TC-03.E6); có lịch đóng đợt thì tổng các đợt = phải thu và hạn = hạn đợt cuối.
    /// Phải thu = 0 (khấu trừ đủ) → đã đóng ngay (E3).
    /// </summary>
    public static Result<Invoice> Issue(string code, Guid enrollmentId, Guid studentId, Guid classId, BranchId branchId, Money amount,
        Money discount, DiscountId? discountId, DateOnly dueDate, string transferContent, DateTimeOffset at,
        InstallmentPlanId? planId = null, IReadOnlyList<InstallmentDue>? schedule = null)
    {
        if (discount > amount) return InvoiceErrors.DiscountTooLarge;
        var parts = schedule?.OrderBy(p => p.DueDate).ToList() ?? [];
        if (parts.Count > 0 && (parts.Any(p => p.Amount <= Money.Zero) || Sum(parts.Select(p => p.Amount)) != amount - discount))
            return InvoiceErrors.InvalidSchedule;
        var inv = new Invoice
        {
            Id = InvoiceId.New(), Code = code, EnrollmentId = enrollmentId, StudentId = studentId, ClassId = classId, BranchId = branchId,
            Amount = amount, Discount = discount, DiscountId = discountId, InstallmentPlanId = parts.Count > 0 ? planId : null, Schedule = parts,
            DueDate = parts.Count > 0 ? parts[^1].DueDate : dueDate, TransferContent = transferContent, IssuedAt = at, Status = InvoiceStatus.Open,
        };
        inv.Raise(new InvoiceIssued(inv.Id, enrollmentId, studentId, branchId, inv.Total, inv.DueDate));
        if (inv.Total == Money.Zero) inv.Raise(new InvoicePaid(inv.Id, studentId, branchId));
        return inv;
    }

    /// <summary>Tình trạng + số liệu tại ngày <paramref name="today"/> (giờ VN)</summary>
    public InvoiceSummary Summarize(DateOnly today)
    {
        var remaining = Remaining;
        var due = CurrentDueDate();
        var status = Status == InvoiceStatus.Voided ? TuitionStatus.Voided
            : RefundedAmount > Money.Zero ? TuitionStatus.Refunded
            : Status == InvoiceStatus.Cancelled ? TuitionStatus.Cancelled
            : remaining == Money.Zero ? TuitionStatus.Paid
            : due is { } d && today > d ? TuitionStatus.Overdue
            : Collected > Money.Zero ? TuitionStatus.Partial
            : TuitionStatus.Unpaid;
        return new InvoiceSummary(Total, Collected, RefundedAmount, PaidAmount, remaining, Refundable, status, due);
    }

    /// <summary>
    /// Hạn đang áp dụng: 1 lần → <see cref="DueDate"/>; nhiều đợt → hạn đợt đầu tiên mà tổng các đợt tới đó vượt số đã thu (TC-02.E3).
    /// Hết nợ / đã hủy → null.
    /// </summary>
    public DateOnly? CurrentDueDate()
    {
        if (Remaining == Money.Zero) return null;
        if (Schedule.Count == 0) return DueDate;
        var collected = Collected;
        var cumulative = Money.Zero;
        foreach (var part in Schedule)
        {
            cumulative += part.Amount;
            if (cumulative > collected) return part.DueDate;
        }
        return DueDate;
    }

    /// <summary>
    /// TC-06 — ghi nhận 1 lần thu (≤ còn nợ). <paramref name="collectorId"/> null = hệ thống tự khớp giao dịch ngân hàng.
    /// Hết nợ → phát <see cref="InvoicePaid"/>.
    /// </summary>
    public Result<Payment> Pay(Money amount, PaymentMethod method, DateTimeOffset at, Guid? collectorId, Guid? bankTransactionId = null,
        string? note = null)
    {
        if (Status != InvoiceStatus.Open) return InvoiceErrors.Closed;
        if (amount <= Money.Zero) return InvoiceErrors.InvalidAmount;
        if (amount > Remaining) return InvoiceErrors.Overpay;
        var p = new Payment(amount, method, at, collectorId, bankTransactionId, Clean(note));
        _payments.Add(p);
        Raise(new PaymentRecorded(Id, p.Id, StudentId, BranchId, amount, method, collectorId, at));
        if (Remaining == Money.Zero) Raise(new InvoicePaid(Id, StudentId, BranchId));
        return p;
    }

    /// <summary>TC-10 — bỏ khớp giao dịch ngân hàng khớp nhầm: xóa các khoản thu của giao dịch đó, trả số tiền đã bỏ</summary>
    public Result<Money> RemoveBankPayments(Guid bankTransactionId)
    {
        var removed = _payments.Where(p => p.BankTransactionId == bankTransactionId).ToList();
        if (removed.Count == 0) return InvoiceErrors.PaymentNotFound;
        foreach (var p in removed) _payments.Remove(p);
        return Sum(removed.Select(p => p.Amount));
    }

    /// <summary>
    /// TC-07 — hoàn tiền: ≤ hoàn tối đa, có lý do. <paramref name="needsApproval"/> (số tiền ≥ ngưỡng Cài đặt — Application quyết)
    /// → tạo yêu cầu chờ duyệt; không → ghi hoàn ngay.
    /// </summary>
    public Result<RefundOutcome> RequestRefund(Money amount, PaymentMethod method, string? reason, Guid by, bool needsApproval, DateTimeOffset at)
    {
        if (Status == InvoiceStatus.Voided) return InvoiceErrors.Closed;
        if (Clean(reason) is not { } why) return InvoiceErrors.ReasonRequired;
        if (amount <= Money.Zero || amount > Refundable) return InvoiceErrors.InvalidRefund;
        if (needsApproval)
        {
            var request = new InvoiceRequest(InvoiceRequestKind.Refund, amount, method, why, by, at);
            _requests.Add(request);
            return new RefundOutcome(null, request);
        }
        return new RefundOutcome(AddRefund(amount, method, why, by, null, at), null);
    }

    /// <summary>TC-08 — đề nghị hủy hóa đơn lập sai (luôn chờ người thứ 2 duyệt). Còn tiền đã thu chưa hoàn → chặn (E1).</summary>
    public Result<InvoiceRequest> RequestVoid(string? reason, Guid by, DateTimeOffset at)
    {
        if (Status == InvoiceStatus.Voided) return InvoiceErrors.Closed;
        if (Clean(reason) is not { Length: >= MinVoidReason } why) return InvoiceErrors.VoidReasonTooShort;
        if (PaidAmount > Money.Zero) return InvoiceErrors.HasPayments;
        if (_requests.Any(r => r.IsPending && r.Kind == InvoiceRequestKind.Void)) return InvoiceErrors.PendingApproval;
        var request = new InvoiceRequest(InvoiceRequestKind.Void, null, null, why, by, at);
        _requests.Add(request);
        return request;
    }

    /// <summary>Gắn yêu cầu với phiếu duyệt chung của System (hộp duyệt)</summary>
    public Result LinkApproval(InvoiceRequestId requestId, Guid approvalId)
    {
        if (Find(requestId) is not { } r) return InvoiceErrors.RequestNotFound;
        r.LinkApproval(approvalId);
        return Result.Success();
    }

    /// <summary>
    /// Người thứ 2 duyệt (khác người tạo — E2): hoàn → ghi hoàn (kiểm lại hoàn tối đa); hủy → hóa đơn "đã hủy" (kiểm lại chưa có tiền).
    /// </summary>
    public Result ApproveRequest(InvoiceRequestId requestId, Guid approverId, DateTimeOffset at)
    {
        if (Find(requestId) is not { } r) return InvoiceErrors.RequestNotFound;
        if (!r.IsPending) return InvoiceErrors.RequestNotPending;
        if (approverId == r.RequestedBy) return InvoiceErrors.SelfApproval;
        if (Status == InvoiceStatus.Voided) return InvoiceErrors.Closed;
        if (r.Kind == InvoiceRequestKind.Refund)
        {
            // Hoàn tối đa không tính chính yêu cầu này (đang nằm trong "chờ duyệt")
            if (r.Amount is not { } amount || amount + (PendingRefunds - amount) > PaidAmount) return InvoiceErrors.InvalidRefund;
            r.Decide(InvoiceRequestStatus.Approved, approverId, null, at);
            AddRefund(amount, r.Method ?? PaymentMethod.Cash, r.Reason, r.RequestedBy, approverId, at);
            return Result.Success();
        }
        if (PaidAmount > Money.Zero) return InvoiceErrors.HasPayments;
        r.Decide(InvoiceRequestStatus.Approved, approverId, null, at);
        (Status, VoidedAt, VoidReason, VoidRequestedBy, VoidApprovedBy) = (InvoiceStatus.Voided, at, r.Reason, r.RequestedBy, approverId);
        // Yêu cầu hoàn còn chờ trên hóa đơn đã hủy → không còn ý nghĩa
        foreach (var other in _requests.Where(x => x.IsPending)) other.Decide(InvoiceRequestStatus.Rejected, approverId, "Hóa đơn đã hủy", at);
        Raise(new InvoiceVoided(Id, StudentId, BranchId, r.Reason));
        return Result.Success();
    }

    public Result RejectRequest(InvoiceRequestId requestId, Guid by, string? note, DateTimeOffset at)
    {
        if (Find(requestId) is not { } r) return InvoiceErrors.RequestNotFound;
        if (!r.IsPending) return InvoiceErrors.RequestNotPending;
        if (by == r.RequestedBy) return InvoiceErrors.SelfApproval;
        r.Decide(InvoiceRequestStatus.Rejected, by, Clean(note), at);
        return Result.Success();
    }

    /// <summary>TC-07.E3 — người tạo rút lại yêu cầu</summary>
    public Result WithdrawRequest(InvoiceRequestId requestId, Guid by, DateTimeOffset at)
    {
        if (Find(requestId) is not { } r) return InvoiceErrors.RequestNotFound;
        if (!r.IsPending) return InvoiceErrors.RequestNotPending;
        if (by != r.RequestedBy) return InvoiceErrors.OnlyRequester;
        r.Decide(InvoiceRequestStatus.Withdrawn, by, null, at);
        return Result.Success();
    }

    /// <summary>TC-08.E4 — khôi phục từ Thùng rác: bỏ thông tin hủy, số hóa đơn giữ nguyên, trạng thái tính lại</summary>
    public Result RestoreVoided()
    {
        if (Status != InvoiceStatus.Voided) return InvoiceErrors.NotVoided;
        (Status, VoidedAt, VoidReason, VoidRequestedBy, VoidApprovedBy) =
            (CancelledAt is null ? InvoiceStatus.Open : InvoiceStatus.Cancelled, null, null, null, null);
        return Result.Success();
    }

    /// <summary>Hủy phần còn lại (HV nghỉ / chuyển lớp / lớp hủy) → hết công nợ, tiền đã thu giữ nguyên để hoàn / khấu trừ</summary>
    public Result CancelRemaining(string? reason, DateTimeOffset at)
    {
        if (Status != InvoiceStatus.Open) return InvoiceErrors.Closed;
        if (Clean(reason) is not { } why) return InvoiceErrors.ReasonRequired;
        (Status, CancelledAt, CancelReason) = (InvoiceStatus.Cancelled, at, why);
        Raise(new InvoiceCancelled(Id, StudentId, BranchId, why));
        return Result.Success();
    }

    private Refund AddRefund(Money amount, PaymentMethod method, string reason, Guid by, Guid? approvedBy, DateTimeOffset at)
    {
        var refund = new Refund(amount, method, reason, by, approvedBy, at);
        _refunds.Add(refund);
        Raise(new RefundIssued(Id, refund.Id, StudentId, BranchId, amount, at));
        return refund;
    }

    private InvoiceRequest? Find(InvoiceRequestId id) => _requests.FirstOrDefault(r => r.Id == id);

    private static Money Sum(IEnumerable<Money> items) => items.Aggregate(Money.Zero, (s, m) => s + m);

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
