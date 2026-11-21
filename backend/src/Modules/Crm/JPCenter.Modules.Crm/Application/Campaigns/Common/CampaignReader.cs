using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Campaigns;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Campaigns.Common;

/// <summary>Dựng DTO chiến dịch + số liệu (số lead, chốt — mọi lead của chiến dịch, kể cả đã ẩn) — tính khi đọc (CRM-16 bước 3)</summary>
public sealed class CampaignReader(ICrmDbContext db, CrmAccess access, PeopleNames people)
{
    /// <summary>Phạm vi chi nhánh: chiến dịch có ít nhất 1 chi nhánh được xem</summary>
    public async Task<List<Campaign>> VisibleAsync(IQueryable<Campaign> q, PermissionAction action, CancellationToken ct)
    {
        var list = await q.ToListAsync(ct);
        return access.Branches(CrmAccess.Campaigns, action) is { } allowed ? list.Where(c => c.BranchIds.Any(b => allowed.Contains(b.Value))).ToList() : list;
    }

    public async Task<IReadOnlyList<CampaignDto>> ToDtosAsync(IReadOnlyList<Campaign> list, CancellationToken ct)
    {
        if (list.Count == 0) return [];
        var ids = list.Select(c => (CampaignId?)c.Id).ToList();
        var counts = await db.Leads.AsNoTracking().Where(l => ids.Contains(l.CampaignId)).GroupBy(l => l.CampaignId)
            .Select(g => new { g.Key, Leads = g.Count(), Won = g.Count(l => l.Status == LeadStatus.Won) }).ToListAsync(ct);
        var keys = list.Select(c => c.Id).ToList();
        var meta = await db.Campaigns.AsNoTracking().Where(c => keys.Contains(c.Id))
            .Select(c => new { c.Id, Created = EF.Property<DateTimeOffset>(c, "CreatedAt"), Version = EF.Property<uint>(c, "xmin") })
            .ToDictionaryAsync(x => x.Id, ct);
        var names = await people.OfAsync(list.Select(c => (Guid?)c.OwnerId), ct);
        return list.Select(c =>
        {
            var n = counts.FirstOrDefault(x => x.Key == c.Id);
            var s = CampaignStats.Of(c, n?.Leads ?? 0, n?.Won ?? 0);
            var m = meta.GetValueOrDefault(c.Id);
            return new CampaignDto(c.Id.Value, c.Code, c.Name, c.Channel, c.BranchIds.Select(b => b.Value).ToList(), c.StartDate, c.EndDate,
                c.Budget.Amount, c.Spent.Amount, c.TargetLeads, c.Status, c.OwnerId, names.GetValueOrDefault(c.OwnerId) ?? "—", c.Utm, c.CourseIds,
                c.Note, m?.Created ?? default,
                new CampaignStatsDto(s.Leads, s.Won, s.TargetPct, s.WinRate, s.CostPerLead?.Amount, s.CostPerWin?.Amount), m?.Version ?? 0);
        }).ToList();
    }

    public async Task<CampaignDto> ToDtoAsync(Campaign c, CancellationToken ct) => (await ToDtosAsync([c], ct))[0];
}
