namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>Tình trạng hóa đơn (suy ra từ thanh toán, lưu để lọc nhanh)</summary>
public enum InvoiceStatus
{
    /// <summary>Chưa đóng</summary>
    Unpaid,
    /// <summary>Đóng một phần</summary>
    Partial,
    /// <summary>Đã đóng đủ</summary>
    Paid,
    /// <summary>Quá hạn</summary>
    Overdue,
    /// <summary>Đã hoàn tiền</summary>
    Refunded,
    /// <summary>Đã hủy phần còn lại</summary>
    Cancelled,
    /// <summary>Đã hủy hóa đơn (lập sai)</summary>
    Voided,
}
