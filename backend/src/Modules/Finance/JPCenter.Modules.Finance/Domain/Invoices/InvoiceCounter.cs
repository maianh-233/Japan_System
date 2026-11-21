using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Finance.Domain.Invoices;

/// <summary>
/// Bộ đếm số hóa đơn theo chi nhánh + kỳ đánh số (TC-04.E4) — 2 hóa đơn lập cùng lúc không trùng số, số đã phát hành không tái sử dụng.
/// Lấy số mới bằng 1 câu cập nhật nguyên tử ở Infrastructure (khóa dòng). Bảng: finance.invoice_counters, UQ (branch_id, period)
/// </summary>
public sealed class InvoiceCounter : Entity<InvoiceCounterId>
{
    private InvoiceCounter() { }

    public BranchId BranchId { get; private set; }
    /// <summary>Kỳ đánh số: <c>yyyy-MM</c> · <c>yyyy</c> · <c>all</c> (<see cref="InvoiceCode.PeriodKey"/>)</summary>
    public string Period { get; private set; } = null!;
    /// <summary>Số thứ tự đã phát hành gần nhất</summary>
    public int Last { get; private set; }

    public static InvoiceCounter Start(BranchId branchId, string period, int last = 0) =>
        new() { Id = InvoiceCounterId.New(), BranchId = branchId, Period = period, Last = last };

    /// <summary>Số kế tiếp (dùng khi nạp dữ liệu / test — đường chính dùng câu lệnh nguyên tử)</summary>
    public int Next() => ++Last;
}
