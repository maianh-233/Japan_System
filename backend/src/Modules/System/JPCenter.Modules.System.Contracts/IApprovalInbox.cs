using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>
/// Hộp duyệt 2 người chung (<c>system.approval_requests</c>) cho module khác: Finance mở phiếu hoàn tiền / hủy hóa đơn (TC-07, TC-08).
/// System tự lưu phiếu; khi người thứ 2 duyệt / từ chối / người tạo rút lại, System gọi <c>Finance.Contracts.IInvoiceApprovals</c> trong cùng request.
/// </summary>
public interface IApprovalInbox
{
    /// <summary>Mở phiếu chờ duyệt (người tạo = người đang gọi). Phiếu đã lưu khi hàm trả về.</summary>
    Task<ApprovalTicket> OpenAsync(ApprovalOpening opening, CancellationToken ct);
}
