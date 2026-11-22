using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Contracts;

/// <summary>
/// System gọi khi phiếu hoàn tiền / hủy hóa đơn ở hộp duyệt chung được quyết (TC-07, TC-08): Finance ghi hoàn / hủy hóa đơn (kiểm lại hoàn tối đa,
/// còn tiền chưa hoàn…) và tự lưu. Thất bại → System không đổi trạng thái phiếu. Người quyết = người đang gọi.
/// </summary>
public interface IInvoiceApprovals
{
    /// <param name="approvalId">System.ApprovalRequest</param>
    Task<Result> DecideAsync(Guid approvalId, bool approve, string? note, CancellationToken ct);

    /// <summary>Người tạo rút lại phiếu (TC-07.E3)</summary>
    Task<Result> WithdrawAsync(Guid approvalId, CancellationToken ct);
}
