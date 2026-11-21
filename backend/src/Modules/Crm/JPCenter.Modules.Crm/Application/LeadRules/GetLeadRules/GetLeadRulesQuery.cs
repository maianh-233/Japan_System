using JPCenter.Application.Common.Messaging;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.LeadRules.Common;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.GetLeadRules;

/// <summary>CRM-09 bước 1 — quy tắc theo thứ tự ưu tiên (quy tắc áp cho mọi chi nhánh nên không lọc theo phạm vi chi nhánh)</summary>
public sealed record GetLeadRulesQuery : IQuery<IReadOnlyList<LeadRuleDto>>;

public sealed class GetLeadRulesHandler(ICrmDbContext db, LeadRuleReader reader) : IQueryHandler<GetLeadRulesQuery, IReadOnlyList<LeadRuleDto>>
{
    public async Task<Result<IReadOnlyList<LeadRuleDto>>> Handle(GetLeadRulesQuery query, CancellationToken ct) =>
        Result.Success(await reader.ToDtosAsync(await db.LeadRules.AsNoTracking().OrderBy(r => r.Priority).ToListAsync(ct), ct));
}
