namespace JPCenter.Modules.Crm.Contracts.Dtos;

/// <summary>
/// Sale phụ trách các lead liên kết với 1 học viên — Finance chọn Sale ghi nhận doanh số khi lập hóa đơn (TC-04.E5).
/// </summary>
/// <param name="WonLeadSaleId">Sale của lead ĐÃ CHỐT (mới nhất nếu nhiều) — Hr.Employee</param>
/// <param name="LatestLeadSaleId">Sale của lead liên kết mới tạo nhất (có người phụ trách) — Hr.Employee</param>
public sealed record StudentLeadSales(Guid? WonLeadSaleId, Guid? LatestLeadSaleId)
{
    public static readonly StudentLeadSales None = new(null, null);
}
