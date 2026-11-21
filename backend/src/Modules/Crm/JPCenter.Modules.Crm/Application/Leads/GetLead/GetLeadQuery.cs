using JPCenter.Application.Common.Messaging;
using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Application.Leads.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.SharedKernel.Primitives;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.GetLead;

/// <summary>CRM-01 bước 4 — ngăn chi tiết: thông tin + lịch sử tư vấn + HV liên kết (lead đã ẩn mở được qua link).</summary>
public sealed record GetLeadQuery(Guid Id) : IQuery<LeadDetailDto>;

public sealed class GetLeadHandler(ICrmDbContext db, CrmAccess access, LeadReader reader) : IQueryHandler<GetLeadQuery, LeadDetailDto>
{
    public async Task<Result<LeadDetailDto>> Handle(GetLeadQuery query, CancellationToken ct)
    {
        var lead = await access.Scope(db.Leads.AsNoTracking(), PermissionAction.View).FirstOrDefaultAsync(l => l.Id == new LeadId(query.Id), ct);
        return lead is null ? LeadErrors.NotFound : await reader.DetailAsync(lead, ct);
    }
}
