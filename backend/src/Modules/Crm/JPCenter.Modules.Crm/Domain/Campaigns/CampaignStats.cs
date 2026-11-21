using JPCenter.SharedKernel.Primitives;

namespace JPCenter.Modules.Crm.Domain.Campaigns;

/// <summary>
/// Số liệu chiến dịch tính khi đọc (CRM-16 bước 3). Chưa có lead / chốt → chỉ số chia là null, giao diện hiện "—" (E3).
/// </summary>
/// <param name="TargetPct">% chỉ tiêu lead (null khi không đặt chỉ tiêu)</param>
/// <param name="WinRate">% chốt / lead</param>
public sealed record CampaignStats(int Leads, int Won, decimal? TargetPct, decimal? WinRate, Money? CostPerLead, Money? CostPerWin)
{
    public static CampaignStats Of(Campaign c, int leads, int won) => new(
        leads,
        won,
        c.TargetLeads is { } t && t > 0 ? Math.Round(leads * 100m / t, 1) : null,
        leads > 0 ? Math.Round(won * 100m / leads, 1) : null,
        leads > 0 ? Money.FromTrusted(Math.Round(c.Spent.Amount / leads, 0, MidpointRounding.AwayFromZero)) : null,
        won > 0 ? Money.FromTrusted(Math.Round(c.Spent.Amount / won, 0, MidpointRounding.AwayFromZero)) : null);
}
