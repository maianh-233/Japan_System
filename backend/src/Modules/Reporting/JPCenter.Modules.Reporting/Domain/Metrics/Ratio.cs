namespace JPCenter.Modules.Reporting.Domain.Metrics;

/// <summary>Tỷ lệ dùng trong báo cáo (user story 17, quy tắc vàng 5): mẫu số 0 → không có số (null), không phải 0.</summary>
public static class Ratio
{
    /// <summary>Phần trăm làm tròn số nguyên, 0,5 làm tròn lên (giống <c>Math.round</c> của frontend)</summary>
    public static int? Percent(decimal part, decimal whole) =>
        whole == 0 ? null : (int)decimal.Round(part * 100m / whole, 0, MidpointRounding.AwayFromZero);

    /// <summary>% thay đổi so với kỳ trước, 1 chữ số thập phân (BC-01 thực thu). Kỳ trước 0 → null (BC-01.E1).</summary>
    public static decimal? Growth(decimal current, decimal previous) =>
        previous == 0 ? null : decimal.Round((current - previous) * 100m / previous, 1, MidpointRounding.AwayFromZero);

    /// <summary>% thay đổi làm tròn số nguyên (BC-01 lead mới). Kỳ trước 0 → null.</summary>
    public static int? GrowthPercent(decimal current, decimal previous) =>
        previous == 0 ? null : (int)decimal.Round((current - previous) * 100m / previous, 0, MidpointRounding.AwayFromZero);

    /// <summary>Chi phí trên mỗi đơn vị (chi phí / lead, chi phí / HV chốt) — làm tròn về đồng. Không có đơn vị nào → null.</summary>
    public static decimal? CostPer(decimal spent, int count) =>
        count == 0 ? null : decimal.Round(spent / count, 0, MidpointRounding.AwayFromZero);
}
