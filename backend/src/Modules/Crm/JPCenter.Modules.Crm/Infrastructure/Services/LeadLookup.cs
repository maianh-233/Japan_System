using JPCenter.Infrastructure.Common.Persistence.Conversions;
using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Contracts.Dtos;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ILeadLookup"/> — lead đã xóa (Thùng rác) không trả.</summary>
internal sealed class LeadLookup(CrmDbContext db) : ILeadLookup
{
    public async Task<IReadOnlyDictionary<Guid, LeadBrief>> FindManyAsync(IReadOnlyCollection<Guid> leadIds, CancellationToken ct)
    {
        var ids = leadIds.Distinct().Select(x => new LeadId(x)).ToList();
        if (ids.Count == 0) return new Dictionary<Guid, LeadBrief>();
        var leads = await db.Leads.AsNoTracking().Where(l => ids.Contains(l.Id)).ToListAsync(ct);
        return leads.ToDictionary(l => l.Id.Value, ToBrief);
    }

    public async Task<IReadOnlyList<LeadBrief>> OpenOfBranchAsync(Guid branchId, int limit, CancellationToken ct)
    {
        var branch = new BranchId(branchId);
        var open = Lead.OpenStatuses.ToList();
        var leads = await db.Leads.AsNoTracking()
            .Where(l => l.BranchId == branch && !l.Hidden && open.Contains(l.Status))
            .OrderByDescending(l => l.LastContactAt)
            .Take(limit)
            .ToListAsync(ct);
        return leads.Select(ToBrief).ToList();
    }

    private static LeadBrief ToBrief(Lead l) => new(l.Id.Value, l.Code, l.FullName, l.Phone, l.BranchId.Value,
        SnakeCaseEnumConverter<LeadStatus>.ToText(l.Status), l.AssigneeId, l.IsOpen && !l.Hidden);
}
