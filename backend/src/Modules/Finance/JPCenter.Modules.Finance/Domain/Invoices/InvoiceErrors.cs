using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

public static class InvoiceErrors
{
    public static readonly Error NotFound = Error.NotFound("finance.invoice.not_found", "Không tìm thấy hóa đơn");
    /// <summary>TC-06.E3, TC-07.E5, TC-08.E3</summary>
    public static readonly Error Closed = Error.Conflict("finance.invoice.closed", "Hóa đơn đã hủy — không thao tác được");
    /// <summary>TC-01.E1, TC-06.E1</summary>
    public static readonly Error InvalidAmount = Error.Validation("finance.invoice.invalid_amount", "Số tiền phải lớn hơn 0");
    /// <summary>TC-03.E6</summary>
    public static readonly Error DiscountTooLarge = Error.Validation("finance.invoice.discount_too_large", "Số tiền giảm lớn hơn học phí");
    /// <summary>TC-06.E2, TC-10.E3</summary>
    public static readonly Error Overpay = Error.Validation("finance.invoice.overpay", "Số tiền vượt quá số còn nợ");
    /// <summary>TC-07.E1</summary>
    public static readonly Error InvalidRefund =
        Error.Validation("finance.invoice.invalid_refund", "Vượt quá số còn hoàn được (đã trừ khoản đang chờ duyệt)");
    public static readonly Error ReasonRequired = Error.Validation("finance.invoice.reason_required", "Nhập lý do");
    /// <summary>TC-08 — lý do hủy hóa đơn ≥ 5 ký tự</summary>
    public static readonly Error VoidReasonTooShort = Error.Validation("finance.invoice.void_reason", "Lý do hủy hóa đơn tối thiểu 5 ký tự");
    /// <summary>TC-07.E2, TC-08.E2</summary>
    public static readonly Error SelfApproval = Error.Forbidden("finance.invoice.self_approval", "Người tạo yêu cầu không tự duyệt");
    /// <summary>TC-08.E1 — hoàn hết tiền đã thu rồi mới hủy hóa đơn</summary>
    public static readonly Error HasPayments =
        Error.Conflict("finance.invoice.has_payments", "Hóa đơn còn tiền đã thu chưa hoàn — hoàn hết trước khi hủy hóa đơn");
    /// <summary>Đã có yêu cầu hủy đang chờ duyệt</summary>
    public static readonly Error PendingApproval = Error.Conflict("finance.invoice.pending_approval", "Hóa đơn đang có yêu cầu chờ duyệt");
    public static readonly Error RequestNotFound = Error.NotFound("finance.invoice.request_not_found", "Không tìm thấy yêu cầu duyệt");
    public static readonly Error RequestNotPending = Error.Conflict("finance.invoice.request_not_pending", "Yêu cầu đã được xử lý");
    public static readonly Error OnlyRequester = Error.Forbidden("finance.invoice.only_requester", "Chỉ người tạo được rút lại yêu cầu");
    public static readonly Error NotVoided = Error.Conflict("finance.invoice.not_voided", "Hóa đơn chưa bị hủy");
    /// <summary>Bỏ khớp giao dịch không có khoản thu tương ứng</summary>
    public static readonly Error PaymentNotFound = Error.NotFound("finance.invoice.payment_not_found", "Không tìm thấy khoản thu của giao dịch này");
    /// <summary>Lịch đóng đợt không khớp số phải thu</summary>
    public static readonly Error InvalidSchedule = Error.Validation("finance.invoice.invalid_schedule", "Các đợt đóng phải cộng lại bằng số phải thu");
}
