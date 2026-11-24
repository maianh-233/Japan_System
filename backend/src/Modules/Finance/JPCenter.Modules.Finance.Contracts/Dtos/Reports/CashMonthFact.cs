namespace JPCenter.Modules.Finance.Contracts.Dtos.Reports;

/// <summary>Thực thu (thu − hoàn, theo ngày thu / ngày hoàn giờ VN) của 1 chi nhánh trong 1 tháng — BC-04 bước 2. Có thể âm (BC-04.E2).</summary>
/// <param name="Month">Ngày 1 của tháng</param>
public sealed record CashMonthFact(Guid BranchId, DateOnly Month, decimal Collected);
