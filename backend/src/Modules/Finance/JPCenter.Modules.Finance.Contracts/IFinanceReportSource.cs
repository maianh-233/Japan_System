using JPCenter.Modules.Finance.Contracts.Dtos.Reports;

namespace JPCenter.Modules.Finance.Contracts;

/// <summary>Số liệu tài chính cho Reporting (user story 17, ADR 0004). <c>branchIds</c> null = mọi chi nhánh — người gọi đã lọc theo quyền.</summary>
public interface IFinanceReportSource
{
    /// <summary>Thực thu theo chi nhánh × tháng, từ ngày 1 tháng <paramref name="from"/> tới hết hôm nay (giờ VN)</summary>
    Task<IReadOnlyList<CashMonthFact>> CashByMonthAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, CancellationToken ct);

    /// <summary>Doanh số ký: tổng phải thu của hóa đơn lập từ <paramref name="from"/> (giờ VN), trừ hóa đơn đã hủy / vô hiệu (BC-04.E1)</summary>
    Task<IReadOnlyList<BranchAmount>> BookedAsync(IReadOnlyCollection<Guid>? branchIds, DateOnly from, CancellationToken ct);

    /// <summary>Hóa đơn đang mở còn nợ tại hôm nay</summary>
    Task<IReadOnlyList<DebtFact>> DebtsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);

    /// <summary>Giao dịch ngân hàng chưa khớp</summary>
    Task<IReadOnlyList<UnmatchedTransactionFact>> UnmatchedTransactionsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct);
}
