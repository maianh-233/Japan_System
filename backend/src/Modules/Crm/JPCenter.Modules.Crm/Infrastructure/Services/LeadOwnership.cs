using JPCenter.Modules.Crm.Contracts;
using JPCenter.Modules.Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Infrastructure.Services;

/// <summary>Hiện thực <see cref="ILeadOwnership"/> — HV liên kết với lead do Sale phụ trách (kể cả lead đã ẩn).</summary>
internal sealed class LeadOwnership(CrmDbContext db) : ILeadOwnership
{
    public async Task<IReadOnlyList<Guid>> StudentIdsOfSaleAsync(Guid employeeId, CancellationToken ct) =>
        await db.Leads.AsNoTracking().IgnoreQueryFilters()
            .Where(l => l.AssigneeId == employeeId)
            .SelectMany(l => l.StudentLinks.Select(k => k.StudentId))
            .Distinct()
            .ToListAsync(ct);
}
