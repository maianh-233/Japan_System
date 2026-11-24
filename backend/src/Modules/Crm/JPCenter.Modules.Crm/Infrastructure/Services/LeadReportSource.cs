using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Contracts.Dtos.Reports;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ILeadReportSource"/> — SLA dùng đúng quy tắc của trang Lead (<see cref="LeadReader.SlaOfAsync"/>).</summary>
internal sealed class LeadReportSource(CrmDbContext db, LeadReader reader, PeopleNames people, IClock clock) : ILeadReportSource
{
    public async Task<IReadOnlyList<LeadFact>> LeadsCreatedAsync(IReadOnlyCollection<Guid>? branchIds, DateTimeOffset from, CancellationToken ct)
    {
        var rows = await InBranches(branchIds)
            .Where(l => !l.Hidden && EF.Property<DateTimeOffset>(l, "CreatedAt") >= from)
            .Select(l => new { l.Id, l.BranchId, Created = EF.Property<DateTimeOffset>(l, "CreatedAt"), l.Status, l.Source, l.AssigneeId })
            .ToListAsync(ct);
        return rows.Select(l => new LeadFact(l.Id.Value, l.BranchId.Value, l.Created, SnakeCaseEnumConverter<LeadStatus>.ToText(l.Status),
            SnakeCaseEnumConverter<LeadSource>.ToText(l.Source), l.AssigneeId)).ToList();
    }

    public async Task<IReadOnlyList<CampaignFact>> CampaignsAsync(IReadOnlyCollection<Guid>? branchIds, CancellationToken ct)
    {
        var campaigns = await db.Campaigns.AsNoTracking().ToListAsync(ct);
        if (branchIds is not null) campaigns = campaigns.Where(c => c.BranchIds.Any(b => branchIds.Contains(b.Value))).ToList();
        if (campaigns.Count == 0) return [];

        var ids = campaigns.Select(c => (Domain.Campaigns.CampaignId?)c.Id).ToList();
        var counts = await db.Leads.AsNoTracking()
            .Where(l => !l.Hidden && ids.Contains(l.CampaignId))
            .GroupBy(l => l.CampaignId)
            .Select(g => new { Id = g.Key, Leads = g.Count(), Won = g.Count(l => l.Status == LeadStatus.Won) })
            .ToListAsync(ct);
        var byId = counts.ToDictionary(c => c.Id!.Value);
        return campaigns.Select(c =>
        {
            var n = byId.GetValueOrDefault(c.Id);
            return new CampaignFact(c.Id.Value, c.Code, c.Name, c.Spent.Amount, c.BranchIds.Select(b => b.Value).ToList(), n?.Leads ?? 0, n?.Won ?? 0);
        }).ToList();
    }

    public async Task<IReadOnlyList<OpenLeadFact>> OpenLeadsAsync(IReadOnlyCollection<Guid>? branchIds, Guid? assigneeId, CancellationToken ct)
    {
        var open = Lead.OpenStatuses.ToList();
        var query = InBranches(branchIds).Where(l => !l.Hidden && open.Contains(l.Status));
        if (assigneeId is { } me) query = query.Where(l => l.AssigneeId == me);
        var leads = await query.ToListAsync(ct);
        if (leads.Count == 0) return [];

        var meta = await reader.MetaAsync(leads.Select(l => l.Id).ToList(), ct);
        var slaOf = await reader.SlaOfAsync(ct);
        var names = await people.OfAsync(leads.Select(l => l.AssigneeId), ct);
        var now = clock.UtcNow;
        return leads.Select(l =>
        {
            var created = meta.GetValueOrDefault(l.Id).Created;
            var (sla, _) = slaOf(l);
            return new OpenLeadFact(l.Id.Value, l.FullName, l.Phone, l.Interest, l.BranchId.Value, l.AssigneeId,
                l.AssigneeId is { } a ? names.GetValueOrDefault(a) : null, SnakeCaseEnumConverter<LeadStatus>.ToText(l.Status), created,
                l.LastContactAt, l.NextFollowUpAt, sla, l.IsSlaOverdue(created, sla, now));
        }).ToList();
    }

    private IQueryable<Lead> InBranches(IReadOnlyCollection<Guid>? branchIds)
    {
        var query = db.Leads.AsNoTracking();
        if (branchIds is null) return query;
        var ids = branchIds.Select(b => new BranchId(b)).ToList();
        return query.Where(l => ids.Contains(l.BranchId));
    }
}
