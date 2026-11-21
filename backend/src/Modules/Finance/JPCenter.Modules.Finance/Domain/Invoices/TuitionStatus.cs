namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Tình trạng học phí của 1 hóa đơn — TÍNH khi đọc, theo thứ tự ưu tiên:
/// đã hủy hóa đơn › đã hoàn (có hoàn) › đã hủy phần còn lại › đã đóng đủ › quá hạn › đóng một phần › chưa đóng.
/// Giá trị JSON khớp <c>TuitionStatus</c> frontend (+ <c>voided</c>).
/// </summary>
public enum TuitionStatus
{
    Unpaid,
    Partial,
    Paid,
    Overdue,
    Refunded,
    Cancelled,
    Voided,
}
