namespace JPCenter.Modules.Finance.Application.Commissions.Common;

/// <summary>Bậc hoa hồng — khớp <c>CommissionTier</c> frontend</summary>
public sealed record CommissionTierDto(decimal From, decimal Percent);

/// <summary>
/// Kết quả tháng của 1 Sale (TC-12 bước 3) — khớp <c>SaleResult</c> frontend (nhân viên rút gọn, <c>deals</c> = danh sách Id hóa đơn).
/// </summary>
/// <param name="Deals">Hóa đơn ghi nhận cho Sale lập trong tháng</param>
/// <param name="Booked">Doanh số ký = Σ phải thu các hóa đơn đó</param>
/// <param name="Collected">Thực thu = Σ thu − Σ hoàn trong tháng (giờ VN; có thể âm — TC-12.E3)</param>
/// <param name="Kpi">Thực thu ÷ chỉ tiêu (0 khi chưa đặt chỉ tiêu)</param>
/// <param name="Rank">Hạng theo thực thu (1 = cao nhất)</param>
public sealed record SaleResultDto(
    Guid EmployeeId,
    string Code,
    string FullName,
    Guid BranchId,
    IReadOnlyList<Guid> Deals,
    decimal Booked,
    decimal Collected,
    decimal Target,
    decimal Kpi,
    CommissionTierDto? Tier,
    decimal Commission,
    int Rank);

/// <summary>Trang Hoa hồng & KPI Sale (TC-12): bảng bậc + kết quả tháng</summary>
public sealed record CommissionBoardDto(string Month, IReadOnlyList<CommissionTierDto> Tiers, IReadOnlyList<SaleResultDto> Results);

/// <summary>Chỉ tiêu tháng của 1 Sale — khớp <c>SaleTarget</c> frontend</summary>
public sealed record SaleTargetDto(Guid EmployeeId, string Month, decimal Target);
