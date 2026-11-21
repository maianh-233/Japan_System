using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Số liệu tính từ thanh toán / hoàn tiền (không lưu) — khớp <c>InvoiceSummary</c> frontend.
/// </summary>
/// <param name="Total">Phải thu = học phí − giảm</param>
/// <param name="Collected">Σ các lần thu</param>
/// <param name="Refunded">Σ các lần hoàn</param>
/// <param name="Paid">Thực thu = đã thu − đã hoàn</param>
/// <param name="Remaining">Còn nợ = phải thu − đã thu (0 khi đã hủy phần còn lại / hủy hóa đơn). Hoàn tiền KHÔNG làm tăng nợ.</param>
/// <param name="Refundable">Hoàn tối đa = thực thu − khoản hoàn đang chờ duyệt (TC-07 bước 1)</param>
/// <param name="CurrentDueDate">Hạn đang áp dụng: hạn đợt gần nhất chưa đóng đủ (TC-02); hết nợ → null</param>
public sealed record InvoiceSummary(Money Total, Money Collected, Money Refunded, Money Paid, Money Remaining, Money Refundable,
    TuitionStatus Status, DateOnly? CurrentDueDate);
