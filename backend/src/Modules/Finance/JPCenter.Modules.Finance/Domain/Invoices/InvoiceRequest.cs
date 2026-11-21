using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Yêu cầu chờ người thứ 2 duyệt trên hóa đơn (maker–checker, quy tắc vàng 3): hoàn tiền từ ngưỡng / hủy hóa đơn.
/// Hiện ở hộp duyệt chung của System (<see cref="ApprovalId"/> = System.ApprovalRequest); khoản hoàn đang chờ trừ vào "hoàn tối đa".
/// Bảng: finance.invoice_requests
/// </summary>
public sealed class InvoiceRequest : Entity<InvoiceRequestId>
{
    private InvoiceRequest() { }

    internal InvoiceRequest(InvoiceRequestKind kind, Money? amount, PaymentMethod? method, string reason, Guid requestedBy, DateTimeOffset at)
        : base(InvoiceRequestId.New()) =>
        (Kind, Amount, Method, Reason, RequestedBy, RequestedAt, Status) = (kind, amount, method, reason, requestedBy, at, InvoiceRequestStatus.Pending);

    public InvoiceRequestKind Kind { get; private set; }
    /// <summary>Hoàn tiền: số tiền</summary>
    public Money? Amount { get; private set; }
    /// <summary>Hoàn tiền: hình thức</summary>
    public PaymentMethod? Method { get; private set; }
    public string Reason { get; private set; } = null!;
    /// <summary>Người tạo yêu cầu (Hr.Employee / tài khoản)</summary>
    public Guid RequestedBy { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }
    public InvoiceRequestStatus Status { get; private set; }
    /// <summary>System.ApprovalRequest — hộp duyệt chung</summary>
    public Guid? ApprovalId { get; private set; }
    public Guid? DecidedBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    /// <summary>Lý do từ chối</summary>
    public string? DecisionNote { get; private set; }

    public bool IsPending => Status == InvoiceRequestStatus.Pending;

    internal void LinkApproval(Guid approvalId) => ApprovalId = approvalId;

    internal void Decide(InvoiceRequestStatus status, Guid? by, string? note, DateTimeOffset at) =>
        (Status, DecidedBy, DecisionNote, DecidedAt) = (status, by, note, at);
}
