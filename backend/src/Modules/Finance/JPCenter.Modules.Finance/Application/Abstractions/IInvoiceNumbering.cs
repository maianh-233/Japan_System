using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Application.Abstractions;

/// <summary>
/// Lấy số thứ tự hóa đơn kế tiếp theo chi nhánh + kỳ đánh số (TC-04.E4) bằng 1 câu lệnh nguyên tử (khóa dòng) — 2 hóa đơn lập cùng lúc
/// không trùng số. Số đã lấy không trả lại (lập hóa đơn lỗi → bỏ trống số đó, không tái sử dụng — quy tắc vàng 4).
/// </summary>
public interface IInvoiceNumbering
{
    Task<int> NextAsync(BranchId branchId, string period, CancellationToken ct);
}
