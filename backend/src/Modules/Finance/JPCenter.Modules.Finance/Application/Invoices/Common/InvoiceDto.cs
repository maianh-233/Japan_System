using JPCenter.Modules.Finance.Domain.Invoices;
using JPCenter.Modules.Finance.Domain.Reminders;

namespace JPCenter.Modules.Finance.Application.Invoices.Common;

/// <summary>
/// Hóa đơn — khớp <c>Invoice</c> ở <c>frontend/src/features/student/types.ts</c> (amount, discount, payments, refunds, cancelled, voided) +
/// số liệu tính sẵn (<c>InvoiceSummary</c>), HV / lớp, lịch đóng đợt, yêu cầu chờ duyệt, lịch sử nhắc (TC-05).
/// </summary>
/// <param name="Status">unpaid · partial · paid · overdue · refunded · cancelled · voided</param>
/// <param name="SaleId">Sale được ghi nhận doanh số (TC-04.E5, TC-12) — chỉ trả ở trang nhân viên, không có ở cổng HV / PH</param>
/// <param name="SaleAssigned">Kế toán đã gán tay (không tự chọn lại)</param>
/// <param name="DueDate">Hạn đang áp dụng (nhiều đợt: đợt đầu chưa đóng đủ); hết nợ → hạn gốc</param>
public sealed record InvoiceDto(
    Guid Id,
    string Code,
    Guid EnrollmentId,
    Guid StudentId,
    string StudentName,
    Guid ClassId,
    string? ClassCode,
    string? ClassName,
    Guid BranchId,
    decimal Amount,
    decimal Discount,
    Guid? DiscountId,
    Guid? InstallmentPlanId,
    IReadOnlyList<InstallmentDueDto> Schedule,
    DateOnly DueDate,
    DateTimeOffset IssuedAt,
    string TransferContent,
    decimal Total,
    decimal Collected,
    decimal Refunded,
    decimal Paid,
    decimal Remaining,
    decimal Refundable,
    TuitionStatus Status,
    IReadOnlyList<PaymentDto> Payments,
    IReadOnlyList<RefundDto> Refunds,
    bool Cancelled,
    string? CancelReason,
    VoidedDto? Voided,
    IReadOnlyList<InvoiceRequestDto> Requests,
    IReadOnlyList<ReminderDto> Reminders,
    Guid? SaleId,
    string? SaleName,
    bool SaleAssigned);

public sealed record InstallmentDueDto(decimal Amount, DateOnly DueDate);

/// <param name="Method">cash · transfer · vietqr · card</param>
/// <param name="Collector">Tên người thu; "Tự động (VietQR)" khi hệ thống tự khớp giao dịch</param>
public sealed record PaymentDto(Guid Id, decimal Amount, PaymentMethod Method, DateTimeOffset PaidAt, string Collector, Guid? CollectorId,
    Guid? BankTransactionId, string? Note);

public sealed record RefundDto(Guid Id, decimal Amount, PaymentMethod Method, DateTimeOffset RefundedAt, string Reason, string By, string? ApprovedBy);

public sealed record VoidedDto(string Reason, string RequestedBy, string ApprovedBy, DateTimeOffset At);

/// <param name="Kind">refund · void</param>
/// <param name="Status">pending · approved · rejected · withdrawn</param>
/// <param name="ApprovalId">Phiếu ở hộp duyệt chung (System.ApprovalRequest)</param>
public sealed record InvoiceRequestDto(Guid Id, InvoiceRequestKind Kind, decimal? Amount, PaymentMethod? Method, string Reason, string RequestedBy,
    DateTimeOffset RequestedAt, InvoiceRequestStatus Status, Guid? ApprovalId, string? DecidedBy, DateTimeOffset? DecidedAt, string? DecisionNote);

/// <param name="Channel">zalo · email · sms · call</param>
/// <param name="By">Tên người nhắc; null = hệ thống tự nhắc (<paramref name="Milestone"/>: before_due · on_due · overdue)</param>
public sealed record ReminderDto(Guid Id, Guid InvoiceId, ReminderChannel Channel, DateTimeOffset At, string? By, string? Note,
    ReminderMilestone? Milestone);
