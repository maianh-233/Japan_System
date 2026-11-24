namespace JPCenter.Modules.Reporting.Domain.Metrics;

/// <summary>Hiệu quả chi phí 1 chiến dịch (BC-03 bước 6)</summary>
/// <param name="CampaignId">Crm.Campaign</param>
/// <param name="Spent">Đã chi (VND)</param>
/// <param name="Leads">Lead gắn chiến dịch (không tính lead ẩn)</param>
/// <param name="Won">Trong số đó, đã chốt</param>
public sealed record CampaignCost(Guid CampaignId, string Code, string Name, decimal Spent, int Leads, int Won)
{
    /// <summary>Chi phí / lead</summary>
    public decimal? CostPerLead => Ratio.CostPer(Spent, Leads);

    /// <summary>Chi phí / HV chốt</summary>
    public decimal? CostPerWon => Ratio.CostPer(Spent, Won);

    /// <summary>Bỏ chiến dịch chưa chi và chưa có lead; rẻ nhất / HV chốt lên đầu, chưa có chốt xếp cuối (theo mã)</summary>
    public static IReadOnlyList<CampaignCost> Rank(IEnumerable<CampaignCost> campaigns) =>
        campaigns.Where(c => c.Leads > 0 || c.Spent > 0)
            .OrderBy(c => c.CostPerWon is null)
            .ThenBy(c => c.CostPerWon)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .ToList();
}
