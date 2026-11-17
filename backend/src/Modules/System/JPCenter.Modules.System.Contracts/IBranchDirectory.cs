using JPCenter.Modules.System.Contracts.Dtos;

namespace JPCenter.Modules.System.Contracts;

/// <summary>Tra cứu chi nhánh — cách DUY NHẤT module khác đọc chi nhánh (tên, trạng thái, tiền tố hóa đơn, tài khoản VietQR).</summary>
public interface IBranchDirectory
{
    Task<BranchSummary?> FindAsync(Guid branchId, CancellationToken ct);

    /// <param name="activeOnly">true = chỉ chi nhánh đang hoạt động (ô chọn khi TẠO dữ liệu mới — CN-08)</param>
    Task<IReadOnlyList<BranchSummary>> ListAsync(bool activeOnly, CancellationToken ct);
}
