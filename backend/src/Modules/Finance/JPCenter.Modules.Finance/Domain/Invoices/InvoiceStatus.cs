namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Trạng thái DO NGƯỜI LÀM của hóa đơn (lưu DB). Tình trạng học phí (chưa đóng / một phần / đã đóng / quá hạn / đã hoàn)
/// TÍNH khi đọc — xem <see cref="TuitionStatus"/> và <see cref="Invoice.Summarize"/>.
/// </summary>
public enum InvoiceStatus
{
    /// <summary>Đang hiệu lực</summary>
    Open,
    /// <summary>Đã hủy phần còn lại (HV nghỉ / chuyển lớp / lớp hủy) — hết công nợ, tiền đã thu giữ nguyên</summary>
    Cancelled,
    /// <summary>Đã hủy hóa đơn lập sai (qua duyệt 2 người)</summary>
    Voided,
}
