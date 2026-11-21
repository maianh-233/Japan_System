using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Paging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.LeadRules.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.GetAssignmentLogs;

/// <summary>CRM-09 bước 3 — nhật ký chia lead (mới nhất trước), lead theo phạm vi xem của trang Chia lead; lọc theo lead.</summary>
public sealed record GetAssignmentLogsQuery(Guid? LeadId, PageRequest Page) : IQuery<PagedResult<AssignmentLogDto>>;

public sealed class GetAssignmentLogsHandler(ICrmDbContext db, CrmAccess access, PeopleNames people)
    : IQueryHandler<GetAssignmentLogsQuery, PagedResult<AssignmentLogDto>>
{
    public async Task<Result<PagedResult<AssignmentLogDto>>> Handle(GetAssignmentLogsQuery query, CancellationToken ct)
    {
        var leads = access.Scope(db.Leads.AsNoTracking().IgnoreQueryFilters(), PermissionAction.View, CrmAccess.LeadRules);
        if (query.LeadId is { } id) leads = leads.Where(l => l.Id == new LeadId(id));
        var q = from log in db.LeadAssignmentLogs.AsNoTracking()
                join lead in leads on log.LeadId equals lead.Id
                join rule in db.LeadRules.AsNoTracking().IgnoreQueryFilters() on log.RuleId equals rule.Id into rules
                from rule in rules.DefaultIfEmpty()
                orderby log.At descending
                select new { log, lead.Code, lead.FullName, RuleName = rule == null ? null : rule.Name };
        var page = query.Page;
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((page.SafePage - 1) * page.SafePageSize).Take(page.SafePageSize).ToListAsync(ct);
        var names = await people.OfAsync(rows.Select(r => r.log.SaleId), ct);
        var items = rows.Select(r => new AssignmentLogDto(r.log.Id.Value, r.log.LeadId.Value, r.Code, r.FullName, r.log.SaleId,
            r.log.SaleId is { } s ? names.GetValueOrDefault(s) : null, r.log.RuleId?.Value, r.RuleName, r.log.At, r.log.Reason)).ToList();
        return new PagedResult<AssignmentLogDto>(items, total, page.SafePage, page.SafePageSize);
    }
}
