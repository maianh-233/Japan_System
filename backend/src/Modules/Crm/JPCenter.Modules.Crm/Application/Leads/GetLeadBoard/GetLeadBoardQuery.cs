using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.GetLeads;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Domain;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.GetLeadBoard;

/// <summary>Số lead mỗi cột Kanban</summary>
public sealed record LeadBoardColumn(LeadStatus Status, int Count);

/// <summary>CRM-01 — đếm lead theo 5 cột Kanban, cùng bộ lọc + phạm vi với danh sách (E1).</summary>
public sealed record GetLeadBoardQuery(LeadFilter Filter) : IQuery<IReadOnlyList<LeadBoardColumn>>;

public sealed class GetLeadBoardHandler(ICrmDbContext db, CrmAccess access, IClock clock) : IQueryHandler<GetLeadBoardQuery, IReadOnlyList<LeadBoardColumn>>
{
    public async Task<Result<IReadOnlyList<LeadBoardColumn>>> Handle(GetLeadBoardQuery query, CancellationToken ct)
    {
        var q = GetLeadsHandler.Apply(access.Scope(db.Leads.AsNoTracking(), PermissionAction.View), query.Filter with { SlaOverdue = false },
            clock.UtcNow);
        var counts = await q.GroupBy(l => l.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        return Enum.GetValues<LeadStatus>().Select(s => new LeadBoardColumn(s, counts.GetValueOrDefault(s))).ToList();
    }
}
