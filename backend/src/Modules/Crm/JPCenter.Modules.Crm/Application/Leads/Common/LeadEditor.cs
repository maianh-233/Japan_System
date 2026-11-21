using JPCenter.Application.Common.Security;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.Leads.Common;

/// <summary>Nạp lead để sửa theo phạm vi <c>crm.leads.update</c> (own = lead mình phụ trách), đặt phiên bản chống sửa đè (CRM-04.E5).</summary>
public sealed class LeadEditor(ICrmDbContext db, CrmAccess access)
{
    public async Task<Lead?> LoadAsync(Guid id, CancellationToken ct, uint? version = null)
    {
        var lead = await access.Scope(db.Leads, PermissionAction.Update).FirstOrDefaultAsync(l => l.Id == new LeadId(id), ct);
        if (lead is not null && version is { } v && v != 0) ((DbContext)db).Entry(lead).Property("xmin").OriginalValue = v;
        return lead;
    }
}
