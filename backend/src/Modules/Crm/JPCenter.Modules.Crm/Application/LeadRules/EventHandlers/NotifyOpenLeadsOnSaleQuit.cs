using JPCenter.Application.Common.IntegrationEvents;
using JPCenter.Modules.Crm.Application.Abstractions;
using JPCenter.Modules.Crm.Application.Common;
using JPCenter.Modules.Crm.Domain.Leads;
using JPCenter.Modules.Hr.Contracts.IntegrationEvents;
using Microsoft.EntityFrameworkCore;

namespace JPCenter.Modules.Crm.Application.LeadRules.EventHandlers;

/// <summary>
/// Nghe <c>EmployeeQuit</c> (Hr): Sale nghỉ việc tự bị bỏ qua khi chia lead (quy tắc giữ nguyên, có cảnh báo — CRM-09.E3);
/// còn lead mở → báo người quản lý chia lead để chuyển tay. Chạy lại chỉ gửi lại thông báo (không đổi dữ liệu).
/// </summary>
public sealed class NotifyOpenLeadsOnSaleQuit(ICrmDbContext db, CrmNotices notices) : IIntegrationEventHandler<EmployeeQuit>
{
    public async Task Handle(EmployeeQuit e, CancellationToken ct)
    {
        var open = await db.Leads.AsNoTracking()
            .Where(l => l.AssigneeId == e.EmployeeId && !l.Hidden && Lead.OpenStatuses.Contains(l.Status))
            .Select(l => l.BranchId.Value).ToListAsync(ct);
        if (open.Count == 0) return;
        notices.SaleQuit(e.FullName, open.Count, open.Distinct().ToList());
        await db.SaveChangesAsync(ct);
    }
}
